#!/bin/bash

# download latest git
git pull

# call ccargo
cargo build -r --manifest-path ./uc/Cargo.toml

chmod +x uc/target/release/uc 
# test if we can put a copy to /usr/bin/ here
sudo cp uc/target/release/uc /usr/bin/

