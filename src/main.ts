import * as core from '@actions/core'

function getInput(key: string) {
  return core.getInput(key, { required: false, trimWhitespace: true })
}

function requireInput(key: string) {
  const value = core.getInput(key, { required: true, trimWhitespace: true })
  if (!value) {
    throw new Error(`The '${key}' input value must be set`)
  }
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

export async function run(): Promise<void> {
  try {
    const username = requireInput('username')
    core.debug(`Input username: ${username}`)

    const packageSource = requireInput('package-source')
    core.debug(`Input package source: ${packageSource}`)

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

    core.setOutput('token-info', JSON.stringify({
      type: 'GitHubActionsV1',
      packageSource,
      audience,
      runtimeToken,
      tokenUrl,
    }))
  } catch (error) {
    if (error instanceof Error) {
      core.setFailed(error.message)
    } else {
      throw error
    }
  }
}
