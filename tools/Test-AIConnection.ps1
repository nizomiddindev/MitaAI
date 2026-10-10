[CmdletBinding()]
param(
    [ValidateSet('Ollama', 'LMStudio', 'OpenRouter', 'Custom')]
    [string]$Provider = 'Ollama',
    [string]$ApiKey = '',
    [string]$Model = '',
    [string]$EndpointUrl = '',
    [string]$Prompt = "Hello Mita, how are you today?"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " MitaAI - AI Connection Diagnostic Tool" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Testing provider: $Provider"

$defaultEndpoints = @{
    'Ollama'     = 'http://localhost:11434/v1/chat/completions'
    'LMStudio'   = 'http://localhost:1234/v1/chat/completions'
    'OpenRouter' = 'https://openrouter.ai/api/v1/chat/completions'
}

$defaultModels = @{
    'Ollama'     = 'llama3.2'
    'LMStudio'   = 'local-model'
    'OpenRouter' = 'meta-llama/llama-3.3-70b-instruct:free'
}

$endpoint = if ([string]::IsNullOrWhiteSpace($EndpointUrl)) { $defaultEndpoints[$Provider] } else { $EndpointUrl }
$modelName = if ([string]::IsNullOrWhiteSpace($Model)) { $defaultModels[$Provider] } else { $Model }

if ([string]::IsNullOrWhiteSpace($endpoint)) {
    Write-Error "Endpoint URL is required for Custom provider."
    exit 1
}

if ($Provider -eq 'OpenRouter' -and [string]::IsNullOrWhiteSpace($ApiKey)) {
    Write-Warning "OpenRouter requires an API key! Pass -ApiKey 'sk-or-v1-...' to test."
    Write-Host "You can get a free key at https://openrouter.ai/keys" -ForegroundColor Yellow
    exit 1
}

Write-Host "Endpoint : $endpoint"
Write-Host "Model    : $modelName"
Write-Host "Prompt   : $Prompt"
Write-Host "`nSending request..." -ForegroundColor Yellow

$systemPrompt = "You are Mita, a friendly, curious, and cheerful companion from the game MiSide. Keep your responses strictly under 2 sentences (maximum 160 characters) so they fit as subtitles. Do not use markdown, actions in asterisks, or emojis. Stay age-appropriate, wholesome, and non-romantic."

$body = [ordered]@{
    model = $modelName
    messages = @(
        [ordered]@{ role = 'system'; content = $systemPrompt },
        [ordered]@{ role = 'user'; content = $Prompt }
    )
    max_tokens = 100
    temperature = 0.7
} | ConvertTo-Json -Depth 5

$headers = @{
    'Content-Type' = 'application/json'
}

if (-not [string]::IsNullOrWhiteSpace($ApiKey)) {
    $headers['Authorization'] = "Bearer $ApiKey"
}

if ($Provider -eq 'OpenRouter') {
    $headers['HTTP-Referer'] = 'https://github.com/MitaAI'
    $headers['X-Title'] = 'MitaAI'
}

$sw = [Diagnostics.Stopwatch]::StartNew()
try {
    $response = Invoke-RestMethod -Uri $endpoint -Method Post -Headers $headers -Body $body -TimeoutSec 30
    $sw.Stop()

    Write-Host "`n[SUCCESS] Response received in $($sw.ElapsedMilliseconds)ms!" -ForegroundColor Green

    if ($response.choices -and $response.choices.Count -gt 0) {
        $rawReply = $response.choices[0].message.content
        Write-Host "`nMita's Reply:" -ForegroundColor Magenta
        Write-Host "----------------------------------------" -ForegroundColor DarkGray
        Write-Host $rawReply -ForegroundColor White
        Write-Host "----------------------------------------" -ForegroundColor DarkGray
    } else {
        Write-Host "Raw response: $($response | ConvertTo-Json -Depth 3)" -ForegroundColor Yellow
    }
}
catch {
    $sw.Stop()
    Write-Host "`n[FAILED] Could not connect to $Provider ($($sw.ElapsedMilliseconds)ms)" -ForegroundColor Red
    Write-Host "Error message: $($_.Exception.Message)" -ForegroundColor Red

    if ($Provider -eq 'Ollama') {
        Write-Host "`nTip: Make sure Ollama is installed and running (`ollama serve` or Ollama desktop app)." -ForegroundColor Cyan
        Write-Host "You can pull a model by opening a terminal and running: ollama run llama3.2" -ForegroundColor Cyan
    }
    elseif ($Provider -eq 'LMStudio') {
        Write-Host "`nTip: Make sure LM Studio is open, a model is loaded, and the local server is started (port 1234)." -ForegroundColor Cyan
    }
    elseif ($Provider -eq 'OpenRouter') {
        Write-Host "`nTip: Make sure your OpenRouter API key is valid and you have internet access." -ForegroundColor Cyan
    }
    exit 1
}
