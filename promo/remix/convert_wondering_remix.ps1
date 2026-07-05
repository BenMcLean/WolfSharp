param(
    [string]$Python = "python"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent (Split-Path -Parent $scriptDir)
$musicTool = Join-Path $repoRoot "src\BenMcLean.Wolf3D.MusicTool\BenMcLean.Wolf3D.MusicTool.csproj"
$midi = Join-Path $scriptDir "Wondering About My Remix.mid"
$op2 = Join-Path $scriptDir "Wondering About My Remix.op2"
$outWlf = Join-Path $repoRoot "godot\BenMcLean.Wolf3D.Shared\Resources\Wondering About My Remix.wlf"
$imfCreatorRepo = "C:\Users\mclea\source\repos\wolf3d\imf-creator"

dotnet run --project $musicTool -- convert-midi-to-wlf `
    --midi $midi `
    --op2 $op2 `
    --out-wlf $outWlf `
    --imfcreator-repo $imfCreatorRepo `
    --python $Python

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
