<#
.SYNOPSIS
  Cria no Azure Boards o Epic, as Features e os Test Cases do Hakari a partir de azure-boards.json.

.EXAMPLE
  .\import-azure.ps1 -DryRun
  .\import-azure.ps1 -Organization minha-org -Project Hakari

.NOTES
  O PAT (escopo Work Items: Read & write) vem de $env:AZDO_PAT, da area de transferencia (-PatFromClipboard,
  que limpa a area depois de ler) ou e pedido no terminal.
  Os itens recebem a tag "hakari-gs"; se ja existirem itens com essa tag o script para, a menos que use -Force.
#>
param(
    [string]$Organization,
    [string]$Project,
    [string]$DataFile = (Join-Path $PSScriptRoot 'azure-boards.json'),
    [switch]$DryRun,
    [switch]$Force,
    [switch]$PatFromClipboard
)

$ErrorActionPreference = 'Stop'
$ApiVersion = '7.1'
$ImportTag = 'hakari-gs'

$data = Get-Content -Raw -Encoding UTF8 $DataFile | ConvertFrom-Json

function Encode([string]$text) {
    [System.Net.WebUtility]::HtmlEncode($text)
}

function Build-Steps($steps) {
    # Formato do campo Microsoft.VSTS.TCM.Steps: XML cujo texto de cada passo e HTML escapado.
    $xml = New-Object System.Text.StringBuilder
    [void]$xml.Append("<steps id=`"0`" last=`"$($steps.Count + 1)`">")
    $id = 2
    foreach ($step in $steps) {
        $action = [Security.SecurityElement]::Escape("<DIV><P>$(Encode $step[0])</P></DIV>")
        $expected = [Security.SecurityElement]::Escape("<DIV><P>$(Encode $step[1])</P></DIV>")
        [void]$xml.Append("<step id=`"$id`" type=`"ValidateStep`">")
        [void]$xml.Append("<parameterizedString isformatted=`"true`">$action</parameterizedString>")
        [void]$xml.Append("<parameterizedString isformatted=`"true`">$expected</parameterizedString>")
        [void]$xml.Append("<description/></step>")
        $id++
    }
    [void]$xml.Append('</steps>')
    $xml.ToString()
}

function Field($name, $value) {
    @{ op = 'add'; path = "/fields/$name"; value = $value }
}

function Link($rel, $url) {
    @{ op = 'add'; path = '/relations/-'; value = @{ rel = $rel; url = $url } }
}

function Gherkin-Html([string]$criteria) {
    "<pre>$(Encode $criteria)</pre>"
}

# Valida o JSON e o XML dos passos antes de falar com o Azure.
foreach ($tc in $data.testCases) {
    if (-not ($data.features.key -contains $tc.feature)) { throw "$($tc.id): feature '$($tc.feature)' nao existe" }
    [void][xml](Build-Steps $tc.steps)
}
Write-Host "Arquivo valido: 1 epic, $($data.features.Count) features, $($data.testCases.Count) test cases."

if ($DryRun) {
    foreach ($f in $data.features) {
        $count = @($data.testCases | Where-Object feature -eq $f.key).Count
        Write-Host ("  {0}  {1} test cases" -f $f.title, $count)
    }
    Write-Host 'Modo simulacao: nada foi enviado ao Azure.'
    return
}

if (-not $Organization -or -not $Project) { throw 'Informe -Organization e -Project.' }

$pat = $env:AZDO_PAT
if ($PatFromClipboard) {
    $pat = (Get-Clipboard -Raw).Trim()
    Set-Clipboard -Value ' '
}
if (-not $pat) {
    $secure = Read-Host 'PAT do Azure DevOps' -AsSecureString
    $pat = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}
$auth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$pat"))
$base = "https://dev.azure.com/$Organization/$([Uri]::EscapeDataString($Project))/_apis/wit"

Add-Type -AssemblyName System.Web.Extensions
# JavaScriptSerializer em vez de ConvertFrom-Json: o do PowerShell 5.1 falha em JSON com chaves que so
# diferem em maiusculas (acontece nos tipos de work item).
$serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$serializer.MaxJsonLength = [int]::MaxValue

function Invoke-Azure($method, $uri, $body, $contentType = 'application/json') {
    # curl.exe forcando IPv4: nesta rede o IPv6 ate o Azure falha de forma intermitente, e o
    # Invoke-RestMethod nao permite escolher a familia de endereco. O PAT vai num arquivo de configuracao
    # temporario (-K), apagado ao fim da chamada, para nao aparecer na linha de comando do processo.
    $responseFile = [IO.Path]::GetTempFileName()
    $configFile = [IO.Path]::GetTempFileName()
    [IO.File]::WriteAllText($configFile, "header = `"Authorization: Basic $auth`"")
    $bodyFile = $null
    $curlArgs = @('-4', '-s', '-K', $configFile, '-X', $method.ToUpper(), '-o', $responseFile, '-w', '%{http_code}', '--max-time', '30')
    if ($body) {
        $bodyFile = [IO.Path]::GetTempFileName()
        [IO.File]::WriteAllText($bodyFile, (ConvertTo-Json -InputObject $body -Depth 10), (New-Object Text.UTF8Encoding $false))
        $curlArgs += @('-H', "Content-Type: $contentType", '--data-binary', "@$bodyFile")
    }
    try {
        for ($attempt = 1; ; $attempt++) {
            $status = & curl.exe @curlArgs $uri
            if ($status -ne '000') {
                if ($LASTEXITCODE -ne 0 -or -not $status) { throw "curl.exe falhou (codigo $LASTEXITCODE) em $method $uri" }
                break
            }
            if ($attempt -ge 3) { throw "Falha de conexao em $method $uri apos 3 tentativas." }
            Write-Host "Falha de conexao, tentando de novo ($attempt/3)..."
            Start-Sleep -Seconds 3
        }
        $text = [IO.File]::ReadAllText($responseFile, [Text.Encoding]::UTF8)
    } finally {
        Remove-Item $responseFile, $configFile -ErrorAction SilentlyContinue
        if ($bodyFile) { Remove-Item $bodyFile -ErrorAction SilentlyContinue }
    }
    # Com PAT invalido o Azure responde 203 ou 302 com a pagina de login em HTML.
    if ($status -in '203', '302' -or $text.TrimStart().StartsWith('<')) {
        throw 'PAT invalido ou sem acesso a esta organizacao (o Azure devolveu a pagina de login).'
    }
    if ($status -notmatch '^2') { throw "Azure respondeu $status em $method $uri : $text" }
    $serializer.DeserializeObject($text)
}

function New-WorkItem($type, $ops) {
    $uri = "$base/workitems/`$$([Uri]::EscapeDataString($type))?api-version=$ApiVersion"
    Invoke-Azure 'Post' $uri $ops 'application/json-patch+json'
}

# Pre-checagens: projeto acessivel, tipo Test Case disponivel, campo de criterios na Feature, importacao repetida.
$projectInfo = Invoke-Azure 'Get' "https://dev.azure.com/$Organization/_apis/projects/$([Uri]::EscapeDataString($Project))?api-version=$ApiVersion"
Write-Host "Projeto encontrado: $($projectInfo.name)"
$types = (Invoke-Azure 'Get' "$base/workitemtypes?api-version=$ApiVersion").value.name
if (-not ($types -contains 'Test Case')) {
    throw "O tipo 'Test Case' nao existe neste projeto. Tipos disponiveis: $($types -join ', ')"
}
if (-not ($types -contains 'Feature')) {
    throw "O tipo 'Feature' nao existe neste projeto (process template diferente de Agile?). Tipos disponiveis: $($types -join ', ')"
}
$featureFields = (Invoke-Azure 'Get' "$base/workitemtypes/Feature/fields?api-version=$ApiVersion").value.referenceName
$featureHasCriteria = $featureFields -contains 'Microsoft.VSTS.Common.AcceptanceCriteria'

$wiql = @{ query = "SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project AND [System.Tags] CONTAINS '$ImportTag'" }
$existing = (Invoke-Azure 'Post' "$base/wiql?api-version=$ApiVersion" $wiql).workItems
if ($existing.Count -gt 0 -and -not $Force) {
    throw "Ja existem $($existing.Count) itens com a tag '$ImportTag'. Apague-os ou rode com -Force para duplicar."
}

$epic = New-WorkItem 'Epic' @(
    (Field 'System.Title' $data.epic.title),
    (Field 'System.Description' (Encode $data.epic.description)),
    (Field 'System.Tags' $ImportTag)
)
Write-Host "Epic #$($epic.id) criado."

$featureUrls = @{}
foreach ($f in $data.features) {
    $ops = @(
        (Field 'System.Title' $f.title),
        (Field 'System.Tags' "$ImportTag; $($f.key)"),
        (Link 'System.LinkTypes.Hierarchy-Reverse' $epic.url)
    )
    if ($featureHasCriteria) {
        $ops += Field 'System.Description' (Encode $f.story)
        $ops += Field 'Microsoft.VSTS.Common.AcceptanceCriteria' (Gherkin-Html $f.criteria)
    } else {
        $ops += Field 'System.Description' "$(Encode $f.story)<h3>Crit&eacute;rios de aceite</h3>$(Gherkin-Html $f.criteria)"
    }
    $created = New-WorkItem 'Feature' $ops
    $featureUrls[$f.key] = $created.url
    Write-Host "Feature #$($created.id) $($f.title)"
}

foreach ($tc in $data.testCases) {
    $description = "<b>Tipo:</b> $(Encode $tc.kind)<br><b>Objetivo:</b> $(Encode $tc.objective)<br><b>Prepara&ccedil;&atilde;o:</b> $(Encode $tc.preparation)"
    $created = New-WorkItem 'Test Case' @(
        (Field 'System.Title' "$($tc.id) $($tc.title)"),
        (Field 'System.Description' $description),
        (Field 'Microsoft.VSTS.TCM.Steps' (Build-Steps $tc.steps)),
        (Field 'Microsoft.VSTS.Common.Priority' $tc.priority),
        (Field 'System.Tags' "$ImportTag; $($tc.feature); $($tc.kind)"),
        (Link 'Microsoft.VSTS.Common.TestedBy-Reverse' $featureUrls[$tc.feature])
    )
    Write-Host "Test Case #$($created.id) $($tc.id) $($tc.title)"
}

Write-Host 'Importacao concluida.'
