# tandoku setup for macOS

## Set up tandoku dependencies

### brew
Follow instructions to Install Homebrew at [brew.sh](https://brew.sh)

### gh
```
brew install gh
```

### dvc
```
brew install dvc
```

### tandoku repo
```
mkdir ~/repos
cd ~/repos
gh repo clone tandoku/tandoku
```

### dotnet
TODO

### pwsh
TODO

### powershell-yaml and yq
```pwsh
Install-Module powershell-yaml

brew install yq
```

### pandoc
```
brew install pandoc
```

### subs2cia
From [subs2cia macOS install](https://github.com/dxing97/subs2cia#macos) but using `pipx`:
```
brew install python ffmpeg pipx
pipx install subs2cia
```

## Build tandoku
```
cd ~/repos/tandoku/src/Tandoku.CommandLine
dotnet build
```
