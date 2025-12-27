# setup_format_and_license.ps1
Write-Host "Setting up formatting and license tools..."

# Install pre-commit (Python)
pip install pre-commit

# Check if Go is installed
if (!(Get-Command go -ErrorAction SilentlyContinue)) {
    Write-Host "Go was not found. Please install Go first from https://go.dev/dl/"
    exit 1
}

# Install addlicense (Go tool)
Write-Host "Installing addlicense..."
go install github.com/google/addlicense@latest

# Register pre-commit hook
Write-Host "Installing pre-commit hook..."
pre-commit install

Write-Host ""
Write-Host "Setup complete!"
Write-Host "------------------------------------"
Write-Host "Next steps:"
Write-Host "  1. Run 'pre-commit run --all-files' once to format everything."
Write-Host "  2. Commit & push your changes."
Write-Host "  3. GitHub Actions will verify on each PR/push."
Write-Host "------------------------------------"
