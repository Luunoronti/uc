use std::env;
use std::net::UdpSocket;
use std::process::exit;

fn main() {
    if env::args().count() > 1 {
        print_help_and_exit();
    }

    let socket: UdpSocket = UdpSocket::bind("0.0.0.0:39912").expect("couldn't bind to address");
    let mut buffer = [0; 1024 * 64];

    //print!("\x1b[?25l");
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
