use std::env;
//use std::fs;
use std::net::UdpSocket;
//use std::path::Path;
use std::process::exit;
//extern crate term_cursor as cursor;
//use terminal_size::{terminal_size, Height, Width};


fn main() {
    if env::args().count() > 1 {
        print_help_and_exit();
    }

    let socket: UdpSocket = UdpSocket::bind("0.0.0.0:39912").expect("couldn't bind to address");
    let mut buffer = [0; 1024 * 64];

    print!("\x1b[?25l");
    loop {
        let size = match socket.recv_from(&mut buffer) {
            Ok(s) => s.0,
            Err(_) => exit(-1),
        };

        let data = &buffer[..size];
        process_msg(&data);
    }
}

fn process_msg(data: &[u8]) {

    let msg_type = data[0];

    if msg_type == 12
    {
        //print!("");
        // new session
        //return;
    }
    let msg = std::str::from_utf8(&data[3..(data.len())]).unwrap();

    let con_color = match msg_type{
        0 => 31, // error
        1 => 31, // assert
        2 => 33, // warning
        3 => 0, // log
        4 => 35, // exception
        12 => 32, // new session
        _ => 0,
    };
    println!("\x1b[{}m{}\x1b[0m", con_color, msg);
}


fn print_help_and_exit() {
    println!("Anafora Standalone Console, build at {} {}, with {}", 
        env!("BUILD_DATE"),
        env!("BUILD_TIME"),
        env!("RUSTC_VERSION"), 
    );

    println!("Usage:");
    println!("\tThis will listen for all output sent to Unity Debug Logs. You can also use it to send commands to the runtime.");
    println!("\t'uc list commands' shows a list of all available commands");
    println!("");
    println!("To download and update new version of uc, clone this Git: https://github.com/Luunoronti/uc");
    println!("You will need to install Rust to build uc.");
    println!("Once cloned, build uc. To do so, change target to anafora_std_console_rust and invoke 'Cargo build -r'.");
    
    println!();
    exit(0);
}
// fn construct_command() -> String {
//     let args: Vec<String> = env::args()
//         .skip(1)
//         .map(|arg: String| {
//             if arg.contains(' ') {
//                 format!("\"{}\"", arg)
//             } else {
//                 arg
//             }
//         })
//         .collect();
//     return args.join(" ");
// }
// fn send_command(command: &String, socket: &UdpSocket) {
//     let cmd_bytes: &[u8] = command.as_bytes();
//     let cmd_len: usize = cmd_bytes.len();
//     let mut buffer: Vec<u8> = vec![0; cmd_len + 3];
//     buffer[3..cmd_len + 3].copy_from_slice(cmd_bytes);
//     buffer[0] = 0x02;
//     buffer[1] = (cmd_len >> 8) as u8;
//     buffer[2] = (cmd_len & 0xff) as u8;

//     let ipendpoint: String =
//         fs::read_to_string("editorconsole.cfg").expect("editor console cfg error");
//     match socket.send_to(&buffer, ipendpoint) {
//         Ok(_sent) => {}
//         Err(_e) => {
//             println!("[Error: {}]", _e);
//             exit(-1);
//         }
//     };
// }
