# Token-based login for NuGet

⚠️ This project is currently in prerelease. It is not supported by NuGet.org at this time.

[![Continuous
integration](https://github.com/joelverhagen/token-login/actions/workflows/ci.yml/badge.svg)](https://github.com/joelverhagen/token-login/actions/workflows/ci.yml)
[![CodeQL](https://github.com/joelverhagen/token-login/actions/workflows/codeql.yml/badge.svg)](https://github.com/joelverhagen/token-login/actions/workflows/codeql.yml)

This project allows you to enable token-based authentication for NuGet operations. Not all NuGet operations, continuous
integration (CI) services, or NuGet package sources are supported.

🛠️ Supported NuGet operations:
- push (e.g. `dotnet nuget push`)
- delete/unlist (e.g. `dotnet nuget delete`)

🚂 Supported CI services:
- GitHub Actions

🎁 Supported NuGet package sources:
- (unreleased) NuGet.org

## Example

### GitHub Actions

```yaml
permissions:
  # enable OIDC tokens in the workflow
  id-token: write
  # not strictly required but allows checkout
  contents: read

jobs:
  push-with-token:
    steps:
      # install the credential provider and set up NUGET_TOKEN_INFO
      - uses: joelverhagen/token-login@v1
        id: token-login
        with:
          username: my-user-account
          package-source: https://my-package-source/v3/index.json

      # run the NuGet operation with NUGET_TOKEN_INFO provided
      - run: dotnet nuget push src/Widget/bin/*.nupkg -s https://my-package-source/v3/index.json
        env:
          NUGET_TOKEN_INFO: ${{ steps.token-login.outputs.token-info }}
```

The first step will install a NuGet credential provider to enable the token-based authorization flow. It will also
provide an output value `token-info` which needs to be passed into any supported NuGet operation as an environment
variable called `NUGET_TOKEN_INFO`. Providing an `id:` value on the first step is essential to allow you to refer to its
outputs in later steps.

The second step is a NuGet operation that will use the provided token info to authorize with the package source. The
`NUGET_TOKEN_INFO: ${{ steps.token-login.outputs.token-info }}` line is critical so the secret token information is
passed into the step that needs it. This allows you to explicitly control which steps should have authorization and
which should not.

## How it works

This credential provider requires a mechanism to fetch tokens at runtime. This mechanism is specific to the CI service.
This is the general workflow:

1. A CI step stores all info needed to acquire tokens into an environment variable `NUGET_TOKEN_INFO`.
2. Another CI step invokes a NuGet operation with `NUGET_TOKEN_INFO` available to the process.
3. NuGet will encounter a `401 Unauthorized` challenge from a package source when it attempts the operation.
4. NuGet will launch the credential provider and request credentials.
5. The credential provider will read `NUGET_TOKEN_INFO` and generate a credential for NuGet.
6. NuGet will retry the package source operation and include the credential.

Step 1 and Step 5 are very specific to each CI service.

### GitHub Actions

GitHub Actions has a way to generate JWTs using special `ACTIONS_ID_TOKEN_REQUEST_URL` and `ACTIONS_RUNTIME_TOKEN`
environment variables. These environment variables are available to the
[@actions/core](https://www.npmjs.com/package/@actions/core) toolkit used for writing custom GitHub Actions. For more
information about how this works, see [OpenID Connect in cloud
providers](https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/configuring-openid-connect-in-cloud-providers).

Essentially. the `ACTIONS_RUNTIME_TOKEN` token can be used to request a token from `ACTIONS_ID_TOKEN_REQUEST_URL`. This
latter token returned by `ACTIONS_ID_TOKEN_REQUEST_URL` will be used to interact with the package source.

The `ACTIONS_RUNTIME_TOKEN` is only used for the `ACTIONS_ID_TOKEN_REQUEST_URL` endpoint and is not usable after the
workflow ends. The token returned by `ACTIONS_ID_TOKEN_REQUEST_URL` will be used to interact with the package source and
only lasts a short time. The latter token is verifiable as being issued by GitHub Action's OIDC issuer
`https://token.actions.githubusercontent.com`. The OIDC issuer has the default OIDC configuration endpoint of
`https://token.actions.githubusercontent.com/.well-known/openid-configuration`. The OIDC configuration is used by the
package source to validate the token it recieves. Most importantly, it contains public information about signing keys
used for the OIDC tokens.

For GitHub Actions, the `NUGET_TOKEN_INFO` value has the following shape:

```json
{
  "type": "GitHubActionsV1",
  "packageSource": "https://my-package-source/v3/index.json",
  "username": "my-user-account",
  "runtimeToken": "<JWT used for tokenUrl>",
  "tokenUrl": "<URL used to get JWTs for the package source>"
}
```

The `type` is used by the credential provider to match the info to the proper token acquistion flow.

The `packageSource` is used by the credential provider so that tokens aren't sent to the wrong package source.

The `username` is used as part of an `audience` query parameter included in the request to `tokenUrl`. This value will
become an `aud` claim in the resulting JWT. The package source uses the `aud` claim to match the token claims to the
associated user account.

The `runtimeToken` is used by the credential provider authorize with the `tokenUrl`.
