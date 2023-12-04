import * as core from '@actions/core'
import * as tc from '@actions/tool-cache'
import * as os from 'os'
import * as path from 'path'
import * as fs from 'fs'

function getInput(key: string, fallback: string) {
  const value = core.getInput(key, { required: false, trimWhitespace: true })
  if (!value) {
    core.debug(`Input '${key}' not set, using: ${fallback}`)
    return fallback
  }
  core.debug(`Input '${key}': ${value}`)
  return value
}

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
    throw new Error(`The '${key}' environment variable must be set. Does this workflow have 'id-token: write' permissions?`)
  }
  return value
}

function getTokenPayload(token: string) {
  const tokenPieces = token.split('.'); // header . payload . signature
  const payload = Buffer.from(tokenPieces[1], 'base64').toString();
  return payload;
}

export const CRED_PROVIDER_URL = "https://aka.ms/nuget/token-login/cred-provider"

export async function run(): Promise<void> {
  try {
    const username = requireInput('username')
    const packageSource = requireInput('package-source')
    const installCredProvider = requireInput('install-cred-provider')
    const credProviderUrl = getInput('cred-provider-url', CRED_PROVIDER_URL)
    const credProviderVersion = credProviderUrl == CRED_PROVIDER_URL ? "from default URL" : "from custom URL"

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

    core.info(`🛠️ NuGet credential provider installation:`);

    const pluginDir = path.join(os.homedir(), '.nuget', 'plugins', 'netcore')
    const providerDestPath = path.join(pluginDir, 'NuGet.TokenCredentialProvider')
    if (installCredProvider.toLowerCase() != 'false') {
      core.info(`  Downloading from ${credProviderUrl}`)
      const providerSrcPath = await tc.downloadTool(credProviderUrl);

      core.info(`  Extracting credential provider to ${providerDestPath}`)
      await tc.extractZip(providerSrcPath, providerDestPath);
      core.info(`  Installed credential provider ${credProviderVersion}`)
      core.setOutput('cred-provider-dir', providerDestPath)
    } else {
      core.info(`  Skipped due to 'install-cred-provider: ${installCredProvider}' input`)
      let warning = true;
      if (!fs.existsSync(pluginDir)) {
        core.info(`  Plugin directory ${pluginDir} does not exist`)
      } else if (fs.readdirSync(pluginDir).length == 0) {
        core.info(`  Plugin directory ${pluginDir} is empty`)
      } else if (!fs.existsSync(providerDestPath)) {
        core.info(`  Credential provider directory ${providerDestPath} does not exist`)
      } else if (fs.readdirSync(providerDestPath).length == 0) {
        core.info(`  Credential provider directory ${providerDestPath} is empty`)
      } else {
        warning = false;
      }

      if (!warning) {
        core.info(`  Credential provider directory ${providerDestPath} exists`)
        core.setOutput('cred-provider-dir', providerDestPath)
      } else {
        core.warning(`No credential provider was found. NuGet authentication may fail.`)
      }
    }

    core.setOutput('token-info', JSON.stringify({
      type: 'GitHubActionsV1',
      packageSource,
      username,
      runtimeToken,
      tokenUrl,
    }))

    core.info(`
✨ GitHub Actions 'token-info' output is ready to be used.
  Package source: ${packageSource}
  Username: ${username}

📖 Ensure the 'token-info' output is provided in NuGet's environment variables as NUGET_TOKEN_INFO. Example:

  - run: dotnet nuget push src/Widget/bin/*.nupkg -s https://api.nuget.org/v3/index.json
    env:
      # Required: update <STEP_ID> to match the id: specified on the 'uses: joelverhagen/token-login' step
      NUGET_TOKEN_INFO: \${{ steps.<STEP_ID>.outputs.token-info }}
      # Optional: enable file logging of the credential provider
      NUGET_TOKEN_LOG_FILE: NuGet.TokenCredentialProvider.log`)
  } catch (error) {
    if (error instanceof Error) {
      core.setFailed(error.message)
    } else {
      throw error
    }
  }
}
