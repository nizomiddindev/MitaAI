# Dot-source this before setup or evaluation so downloads stay on D:.
$VoiceRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../local/voice'))
$env:UV_CACHE_DIR = Join-Path $VoiceRoot 'cache/uv'
$env:UV_PYTHON_INSTALL_DIR = Join-Path $VoiceRoot 'python'
$env:UV_PYTHON_BIN_DIR = Join-Path $VoiceRoot 'python-bin'
$env:UV_LINK_MODE = 'copy'
$env:PIP_CACHE_DIR = Join-Path $VoiceRoot 'cache/pip'
$env:HF_HOME = Join-Path $VoiceRoot 'cache/huggingface'
$env:HF_HUB_CACHE = Join-Path $env:HF_HOME 'hub'
$env:HF_ASSETS_CACHE = Join-Path $env:HF_HOME 'assets'
$env:HF_DATASETS_CACHE = Join-Path $env:HF_HOME 'datasets'
$env:HF_HUB_DISABLE_XET = '1'
$env:HF_HUB_DOWNLOAD_TIMEOUT = '60'
$env:HF_HUB_ETAG_TIMEOUT = '30'
$env:TORCH_HOME = Join-Path $VoiceRoot 'cache/torch'
$env:CUDA_CACHE_PATH = Join-Path $VoiceRoot 'cache/cuda'
$env:NUMBA_CACHE_DIR = Join-Path $VoiceRoot 'cache/numba'
$env:PKUSEG_HOME = Join-Path $VoiceRoot 'cache/pkuseg'
$env:XDG_CACHE_HOME = Join-Path $VoiceRoot 'cache'
$env:MPLCONFIGDIR = Join-Path $VoiceRoot 'cache/matplotlib'
$env:CACHED_PATH_CACHE_ROOT = Join-Path $VoiceRoot 'cache/cached-path'
$env:WANDB_MODE = 'disabled'
$env:WANDB_DIR = Join-Path $VoiceRoot 'cache/wandb'
$env:WANDB_CACHE_DIR = Join-Path $VoiceRoot 'cache/wandb'
$env:TEMP = Join-Path $VoiceRoot 'tmp'
$env:TMP = $env:TEMP
$env:PYTHONNOUSERSITE = '1'
$env:PYTHONUTF8 = '1'
$env:PYTHONPYCACHEPREFIX = Join-Path $VoiceRoot 'cache/python-bytecode'
foreach ($Path in @($VoiceRoot, $env:UV_CACHE_DIR, $env:UV_PYTHON_INSTALL_DIR,
    $env:UV_PYTHON_BIN_DIR, $env:PIP_CACHE_DIR, $env:HF_HOME, $env:HF_HUB_CACHE,
    $env:HF_ASSETS_CACHE, $env:HF_DATASETS_CACHE, $env:TORCH_HOME, $env:CUDA_CACHE_PATH, $env:NUMBA_CACHE_DIR, $env:PKUSEG_HOME,
    $env:MPLCONFIGDIR, $env:CACHED_PATH_CACHE_ROOT, $env:WANDB_DIR, $env:PYTHONPYCACHEPREFIX, $env:TEMP)) {
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}
