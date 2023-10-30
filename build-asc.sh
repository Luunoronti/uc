#!/bin/bash

# download latest git
git pull

# call ccargo
cargo build -r --manifest-path ./asc/Cargo.toml

chmod +x asc/target/release/asc 
# test if we can put a copy to /usr/bin/ here
sudo cp asc/target/release/asc /usr/bin/

