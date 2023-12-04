import * as core from '@actions/core'
import * as tc from '@actions/tool-cache'
import * as os from 'os'
import * as path from 'path'

function requireInput(key: string) {
  const value = core.getInput(key, { required: true, trimWhitespace: true })
  if (!value) {
    throw new Error(`The '${key}' input value must be set`)
  }
  core.debug(`Input '${key}': ${value}`)
  return value
}

function requireEnv(key: string) {
  const value = process.env[key]?.trim()
  if (!value) {
    throw new Error(`The '${key}' environment variable must be set`)
  }
  return value
}

function getTokenPayload(token: string) {
  const tokenPieces = token.split('.'); // header . payload . signature
  const payload = Buffer.from(tokenPieces[1], 'base64').toString();
  return payload;
}

const providerVersion = "v0.7.0"
export const PROVIDER_URL = `https://github.com/joelverhagen/token-login/releases/download/${providerVersion}/NuGet.TokenCredentialProvider.zip`

export async function run(): Promise<void> {
  try {
    const username = requireInput('username')
    const packageSource = requireInput('package-source')
    const installCredentialProvider = requireInput('install-cred-provider')

    const runtimeToken = requireEnv('ACTIONS_RUNTIME_TOKEN')
    core.debug(`Runtime token payload: ${getTokenPayload(runtimeToken)}`)

    const tokenUrl = requireEnv('ACTIONS_ID_TOKEN_REQUEST_URL')
    core.debug(`Token URL: ${tokenUrl}`)

    let url;
    try {
      url = new URL(packageSource);
    } catch {
      throw new Error(`An valid package source URL is required`)
    }

    const audience = `${username}@${url.hostname}`
    core.debug(`Using audience: ${audience}`)

    if (installCredentialProvider.toLowerCase() != 'false') {
      core.info(`Downloading credential provider from ${PROVIDER_URL}`)
      const providerSrcPath = await tc.downloadTool(PROVIDER_URL);
      const providerDestPath = path.join(os.homedir(), '.nuget', 'plugins', 'netcore', 'NuGet.TokenCredentialProvider')

      core.info(`Extracting credential provider to ${providerDestPath}`)
      await tc.extractZip(providerSrcPath, providerDestPath);
    }

    core.setOutput('token-info', JSON.stringify({
      type: 'GitHubActionsV1',
      packageSource,
      audience,
      runtimeToken,
      tokenUrl,
    }))

    core.info(`Done preparing token info for ${username} on ${packageSource}`)
  } catch (error) {
    if (error instanceof Error) {
      core.setFailed(error.message)
    } else {
      throw error
    }
  }
}
