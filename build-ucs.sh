#!/bin/bash

# download latest git
git pull

# call ccargo
cargo build -r --manifest-path ./ucs/Cargo.toml

chmod +x ucs/target/release/ucs 
# test if we can put a copy to /usr/bin/ here
sudo cp ucs/target/release/ucs /usr/bin/

