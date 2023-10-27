#!/bin/bash

# download latest git
git pull

# call ccargo
cargo build -r --manifest-path ./uc_rust/Cargo.toml

chmod +x uc_rust/target/release/uc 
# test if we can put a copy to /usr/bin/ here
sudo cp uc_rust/target/release/uc /usr/bin/

# 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


type -p curl >/dev/null || (sudo apt update && sudo apt install curl -y)
curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg | sudo dd of=/usr/share/keyrings/githubcli-archive-keyring.gpg \
&& sudo chmod go+r /usr/share/keyrings/githubcli-archive-keyring.gpg \
&& echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" | sudo tee /etc/apt/sources.list.d/github-cli.list > /dev/null \
&& sudo apt update \
&& sudo apt install gh -y