$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$parser=Get-Content (Join-Path $root 'src\RimForge.Core\Models\StructuredSearchQuery.cs') -Raw
$filter=Get-Content (Join-Path $root 'src\RimForge.Core\Services\ModFilteringService.cs') -Raw
foreach($name in @('author','source','badge','requires','required-by','active','issue')){if(-not $parser.Contains('"'+$name+'"')){throw "Missing structured search filter: $name"}}
if(-not $filter.Contains('MatchesIdentity')){throw 'Identity search evaluator missing.'}
Write-Host 'Structured search query validation passed.'
