# tandoku setup for Steam Deck

## Set up Edge browser
1. Install Microsoft Edge browser from Discover hub in Desktop mode.
2. Sign in and configure Yomitan or other extensions as needed.
    1. Download and install dictionaries for Yomitan.
    2. Change or turn off the modifier key for dictionary popups from the default of Shift.
3. [Recommended] Under Settings > Start, home, and new tabs > When Edge starts, choose 'Open tabs from the previous session'.
4. Add Edge shortcut to Steam so it can be launched from Game mode.
5. [Recommended] In gamepad configuration for Edge, set L4 to F11 (to toggle full screen mode) and R4 to ESC (for dismissing Yomitan popups).

## Configure screenshots export
1. Enable 'uncompressed' screenshots from [Steam in Desktop mode](https://steamcommunity.com/sharedfiles/filedetails/?id=1726400605).
2. Install [decky-cloud-saves](https://github.com/GedasFX/decky-cloud-save) plugin and configure cloud provider.
    - Note: decky-cloud-save is only used to configure the cloud provider for rclone.
3. Customize cloud path in the following script and save as `tandoku-screenshot-export.sh`
```sh
#!/bin/sh
~/homebrew/plugins/decky-cloud-save/rclone copy ~/Pictures/uncompressed/ backend:tandoku/staging/steam-deck/import/screenshots/ --copy-links --progress
```
4. Run `chmod +x tandoku-screenshot-export.sh` to make script executable.
5. Install [Bash Shortcuts](https://github.com/Tormak9970/bash-shortcuts) plugin and configure shortcut to run the script.

## Configure tandoku content sync
Assuming that screenshots export has been configured already:
1. Customize cloud path in the following script and save as `tandoku-content-sync.sh`
```sh
#!/bin/sh
~/homebrew/plugins/decky-cloud-save/rclone sync backend:tandoku/staging/steam-deck/export/ ~/tandoku/ --progress
```
2. Run `chmod +x tandoku-content-sync.sh` to make script executable.
3. Add read-only permission for Edge browser to /home/deck/tandoku (or use `~/Documents/tandoku` above instead)
4. Configure shortcut in Bash Shortcuts to run the script.

## Set up tandoku dependencies

### brew
Follow instructions to Install Homebrew at [brew.sh](https://brew.sh)

### gh
```
brew install gh
```

### tandoku repo
```
mkdir ~/repos
cd ~/repos
gh repo clone tandoku/tandoku
```

### dotnet
```
brew install dotnet

cat << \EOF >> ~/.bashrc
# Allow apps to find .NET Core SDK
export DOTNET_ROOT="/home/linuxbrew/.linuxbrew/opt/dotnet/libexec"
EOF
```

### pwsh (as dotnet global tool)
```
dotnet tool install --global PowerShell

cat << \EOF >> ~/.bashrc
# Add .NET Core SDK tools
export PATH="$PATH:/home/deck/.dotnet/tools"
EOF
```

### dvc
```
brew install dvc
```

## Build tandoku
```
cd ~/repos/tandoku/src/Tandoku.CommandLine
dotnet build
```