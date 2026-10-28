$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$projects = @(
    'PersistenceManagerTests',
    'PlayerJoinBootstrapTests',
    'ConsoleInputPatchTests',
    'SleepControllerPatchTests',
    'DedicatedRuntimeContextTests',
    'HeadlessMessagingPolicyTests',
    'PublicServerStatusQuerySmoke'
)

foreach ($project in $projects) {
    Write-Host "Running $project"
    dotnet run --project "$repositoryRoot/tests/$project/$project.csproj" --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "$project failed with exit code $LASTEXITCODE"
    }
}

