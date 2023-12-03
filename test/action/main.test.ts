import * as core from '@actions/core'
import * as tc from '@actions/tool-cache'
import * as os from 'os'
import * as path from 'path'
import * as main from '../../src/action/main'

// Mock the action's main function
const runMock = jest.spyOn(main, 'run')

// Other utilities
const timeRegex = /^\d{2}:\d{2}:\d{2}/

// Mock the GitHub Actions core library
let debugMock: jest.SpyInstance
let infoMock: jest.SpyInstance
let errorMock: jest.SpyInstance
let getInputMock: jest.SpyInstance
let setFailedMock: jest.SpyInstance
let setOutputMock: jest.SpyInstance
let downloadToolMock: jest.SpyInstance
let extractZipMock: jest.SpyInstance

describe('action', () => {
  const OLD_ENV = process.env;

  beforeEach(() => {
    jest.resetModules()
    jest.clearAllMocks()
    process.env = { ...OLD_ENV }

    debugMock = jest.spyOn(core, 'debug').mockImplementation()
    infoMock = jest.spyOn(core, 'info').mockImplementation()
    errorMock = jest.spyOn(core, 'error').mockImplementation()
    getInputMock = jest.spyOn(core, 'getInput').mockImplementation()
    setFailedMock = jest.spyOn(core, 'setFailed').mockImplementation()
    setOutputMock = jest.spyOn(core, 'setOutput').mockImplementation()

    downloadToolMock = jest.spyOn(tc, 'downloadTool').mockImplementation()
    extractZipMock = jest.spyOn(tc, 'extractZip').mockImplementation()

    getInputMock.mockImplementation((name: string): string => {
      switch (name) {
        case 'username':
          return 'my-username'
        case 'package-source':
          return 'https://apidev.nugettest.org/v3/index.json'
        case 'install-cred-provider':
          return 'true'
        default:
          return ''
      }
    })

    downloadToolMock.mockImplementation(() => "tool-cache-path")

    process.env.ACTIONS_RUNTIME_TOKEN = `a.${btoa(JSON.stringify({ iss: 'me' }))}.z`
    process.env.ACTIONS_ID_TOKEN_REQUEST_URL = `https://example/my-token-endpoint`
  })

  afterAll(() => {
    process.env = OLD_ENV
  });

  it('sets the token-info output with username', async () => {
    await main.run()

    expect(runMock).toHaveReturned()
    expect(downloadToolMock).toHaveBeenNthCalledWith(1, main.PROVIDER_URL)
    const expectedDest = path.join(os.homedir(), '.nuget', 'plugins', 'netcore', 'NuGet.TokenCredentialProvider')
    expect(extractZipMock).toHaveBeenNthCalledWith(1, 'tool-cache-path', expectedDest)
    expect(setOutputMock).toHaveBeenNthCalledWith(1, 'token-info', JSON.stringify({
      type: "GitHubActionsV1",
      packageSource: "https://apidev.nugettest.org/v3/index.json",
      audience: "my-username@apidev.nugettest.org",
      runtimeToken: "a.eyJpc3MiOiJtZSJ9.z",
      tokenUrl: "https://example/my-token-endpoint",
    }))
    expect(errorMock).not.toHaveBeenCalled()
  })

  it('allows credential provider to not be installed', async () => {
    getInputMock.mockImplementation((name: string): string => {
      switch (name) {
        case 'username':
          return 'my-username'
        case 'package-source':
          return 'https://apidev.nugettest.org/v3/index.json'
        case 'install-cred-provider':
          return 'false'
        default:
          return ''
      }
    })

    await main.run()

    expect(runMock).toHaveReturned()
    expect(downloadToolMock).not.toHaveBeenCalled()
    expect(extractZipMock).not.toHaveBeenCalled()
    expect(setOutputMock).toHaveBeenNthCalledWith(1, 'token-info', JSON.stringify({
      type: "GitHubActionsV1",
      packageSource: "https://apidev.nugettest.org/v3/index.json",
      audience: "my-username@apidev.nugettest.org",
      runtimeToken: "a.eyJpc3MiOiJtZSJ9.z",
      tokenUrl: "https://example/my-token-endpoint",
    }))
    expect(errorMock).not.toHaveBeenCalled()
  })

  it('requires username input', async () => {
    getInputMock.mockImplementation((name: string): string => {
      switch (name) {
        case 'package-source':
          return 'https://apidev.nugettest.org/v3/index.json'
        case 'install-cred-provider':
          return 'true'
        default:
          return ''
      }
    })

    await main.run()

    expect(runMock).toHaveReturned()
    expect(setFailedMock).toHaveBeenNthCalledWith(1, "The 'username' input value must be set")
    expect(setOutputMock).not.toHaveBeenCalled()
    expect(errorMock).not.toHaveBeenCalled()
  })

  it('requires package-source input', async () => {
    getInputMock.mockImplementation((name: string): string => {
      switch (name) {
        case 'username':
          return 'my-username'
        case 'install-cred-provider':
          return 'true'
        default:
          return ''
      }
    })

    await main.run()

    expect(runMock).toHaveReturned()
    expect(setFailedMock).toHaveBeenNthCalledWith(1, "The 'package-source' input value must be set")
    expect(setOutputMock).not.toHaveBeenCalled()
    expect(errorMock).not.toHaveBeenCalled()
  })

  it('requires install-cred-provider input', async () => {
    getInputMock.mockImplementation((name: string): string => {
      switch (name) {
        case 'username':
          return 'my-username'
        case 'package-source':
          return 'https://apidev.nugettest.org/v3/index.json'
        default:
          return ''
      }
    })

    await main.run()

    expect(runMock).toHaveReturned()
    expect(setFailedMock).toHaveBeenNthCalledWith(1, "The 'install-cred-provider' input value must be set")
    expect(setOutputMock).not.toHaveBeenCalled()
    expect(errorMock).not.toHaveBeenCalled()
  })

  it('requires valid package source URL', async () => {
    getInputMock.mockImplementation((name: string): string => {
      switch (name) {
        case 'username':
          return 'my-username'
        case 'package-source':
          return 'invalid'
        case 'install-cred-provider':
          return 'true'
        default:
          return ''
      }
    })

    await main.run()

    expect(runMock).toHaveReturned()
    expect(setFailedMock).toHaveBeenNthCalledWith(1, 'An valid package source URL is required')
    expect(setOutputMock).not.toHaveBeenCalled()
    expect(errorMock).not.toHaveBeenCalled()
  })

  it('requires ACTIONS_RUNTIME_TOKEN env var', async () => {
    process.env.ACTIONS_RUNTIME_TOKEN = ''

    await main.run()

    expect(runMock).toHaveReturned()
    expect(setFailedMock).toHaveBeenNthCalledWith(1, "The 'ACTIONS_RUNTIME_TOKEN' environment variable must be set")
    expect(setOutputMock).not.toHaveBeenCalled()
    expect(errorMock).not.toHaveBeenCalled()
  })

  it('requires ACTIONS_ID_TOKEN_REQUEST_URL env var', async () => {
    process.env.ACTIONS_ID_TOKEN_REQUEST_URL = ''

    await main.run()

    expect(runMock).toHaveReturned()
    expect(setFailedMock).toHaveBeenNthCalledWith(1, "The 'ACTIONS_ID_TOKEN_REQUEST_URL' environment variable must be set")
    expect(setOutputMock).not.toHaveBeenCalled()
    expect(errorMock).not.toHaveBeenCalled()
  })
})
