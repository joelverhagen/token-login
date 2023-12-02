import * as core from '@actions/core'

function requireInput(key: string) {
  const value = core.getInput(key, { required: true, trimWhitespace: true })
  if (!value) {
    throw new Error(`The ${key} input value must be set`)
  }
  return value
}

function requireEnv(key: string) {
  const value = process.env[key]?.trim()
  if (!value) {
    throw new Error(`The ${key} environment variable must be set`)
  }
  return value
}

function getTokenClaims(token: string) {
  const tokenPieces = token.split('.'); // header . payload . signature
  const payload = Buffer.from(tokenPieces[1], 'base64').toString();
  return payload;
}

export async function run(): Promise<void> {
  try {
    const username = requireInput('username')
    core.debug(`Input username: ${username}`)

    const audience = requireInput('audience')
    core.debug(`Input audience: ${audience}`)

    const packageSource = requireInput('package-source')
    core.debug(`Input package source: ${packageSource}`)

    let url;
    try {
      url = new URL(packageSource);
    } catch {
      throw new Error(`An valid HTTPS package source URL is required`)
    }

    if (url.protocol != 'https:') {
      throw new Error(`An HTTPS package source URL is required. The value provided protocol was ${url.protocol}`)
    }

    const runtimeToken = requireEnv('ACTIONS_RUNTIME_TOKEN')
    core.debug(`Runtime token payload: ${getTokenClaims(runtimeToken)}`)

    const tokenUrl = requireEnv('ACTIONS_ID_TOKEN_REQUEST_URL')
    core.debug(`Token URL: ${tokenUrl}`)

    const tokenInfo = { audience, packageSource, runtimeToken, tokenUrl, username }
    core.setOutput('token-info', JSON.stringify(tokenInfo))
  } catch (error) {
    if (error instanceof Error) {
      core.setFailed(error.message)
    } else {
      throw error
    }
  }
}
