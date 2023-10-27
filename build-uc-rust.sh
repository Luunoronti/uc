#!/bin/bash

# download latest git
git pull

# call ccargo
cargo build -r --manifest-path ./uc_rust/Cargo.toml

chmod +x uc_rust/target/release/uc 
# test if we can put a copy to /usr/bin/ here
sudo cp uc_rust/target/release/uc /usr/bin/