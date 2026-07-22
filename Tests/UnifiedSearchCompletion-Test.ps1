$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$parser=Get-Content (Join-Path $root 'src\RimForge.Core\Models\StructuredSearchQuery.cs') -Raw
$search=Get-Content (Join-Path $root 'src\RimForge.App\Features\Search\MainWindow.Search.cs') -Raw
$bar=Get-Content (Join-Path $root 'src\RimForge.App\Features\CommandBar\EngineeringCommandBarView.xaml') -Raw
foreach($token in @('SearchBinaryExpression','SearchNotExpression','GetSuggestions','TokenKind.And','TokenKind.Or')){if(-not $parser.Contains($token)){throw "Missing search parser contract: $token"}}
foreach($token in @('SearchSuggestions','ActiveSearchChips','query.Evaluate')){if(-not $search.Contains($token)){throw "Missing search presentation contract: $token"}}
if($search -notmatch 'SearchSuggestionText' -or $search -notmatch 'ActiveSearchChipText'){throw 'Search suggestions/chips presentation state is missing.'}
Write-Host 'Unified search completion validation passed.'
