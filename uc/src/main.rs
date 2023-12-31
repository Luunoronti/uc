use std::env;
use std::fs;
use std::net::UdpSocket;
use std::path::Path;
use std::process::exit;
extern crate term_cursor as cursor;
use terminal_size::{terminal_size, Height, Width};

static mut PROGRESS_BEGAN: bool = false;
static mut LAST_TITLE_LEN: usize = 0;
static mut LAST_MSG_LEN: usize = 0;
static mut STATUS_LEN: usize = 0;
static mut CUR_X: i32 = 0;
static mut CUR_Y: i32 = 0;

pub mod cpuprogress;
pub mod tools;

fn main() {
    if env::args().count() == 1 {
        print_help_and_exit();
    }

    if Path::new("editorconsole.cfg").is_file() == false {
        return;
    }

    let socket: UdpSocket = UdpSocket::bind("0.0.0.0:0").expect("couldn't bind to address");
    let command = construct_command();
    send_command(&command, &socket);
    let mut buffer = [0; 1024 * 64];

    print!("\x1b[?25l");

    loop {
        let size = match socket.recv_from(&mut buffer) {
            Ok(s) => s.0,
            Err(_) => {
                hide_progress_safe();
                exit(-1)
            }
        };

        let data = &buffer[..size];
        process_resp(&data);
    }
}

fn process_resp(data: &[u8]) {
    let code = data[0];
    unsafe {
        match code {
            0x01 => {
                hide_progress_safe();
                exit(0)
            } // command completed

            0xF0 => {
                hide_progress(); // make sure we hide progress. it may show later, but we must hide it for now
                print!(
                    "{}",
                    std::str::from_utf8(&data[1..(data.len() - 1)]).unwrap()
                )
            } // text message
            0x30 => {}               // ping, do nothing
            0x11 => hide_progress(), // hide progress
            0x10 => show_progress(data),
            _ => println!("{}", code),
        }
    }
}

fn hide_progress_safe() {
    unsafe {
        hide_progress();
    }
}
unsafe fn hide_progress() {
    if PROGRESS_BEGAN == false {
        return;
    }
    PROGRESS_BEGAN = false;
    //let (cx, cy) = cursor::get_pos().expect("Getting the cursor position failed");
    let ttl = LAST_TITLE_LEN + 29;
    let msl = LAST_MSG_LEN + 25;

    print!(
        "{}\n{}\n{}",
        format!("{:<ttl$}", ""),
        format!("{:<msl$}", ""),
        format!("{:<STATUS_LEN$}", "")
    );
    cursor::set_pos(CUR_X, CUR_Y).expect("Set failed again");
}
unsafe fn show_progress(data: &[u8]) {
    let _prog = data[1];
    let title_size: usize = data[4] as usize;
    let msg_size: usize = data[5] as usize;

    let title = std::str::from_utf8(&data[6..(6 + title_size)]).unwrap();
    let msg = std::str::from_utf8(&data[6 + title_size..6 + title_size + msg_size]).unwrap();

    let mut wnd_width: usize = 40;

    if let Some((Width(width), Height(_))) = terminal_size() {
        wnd_width = width as usize;
    } else {
    }

    // store cursor position
    if PROGRESS_BEGAN == false {
        // hide cursor and store its position, add 4 lines, restore cursor position
        let (x, _) = cursor::get_pos().expect("Getting the cursor position failed");
        CUR_X = x;
        if x > 0 {
            CUR_X = x + 1;
        }
        if x > 0 {
            println!();
            println!();
            println!();
            println!();
            //println!();
            let (_, y) = cursor::get_pos().expect("Getting the cursor position failed");
            CUR_Y = y - 4;
        } else {
            println!();
            println!();
            println!();
            //println!();
            let (_, y) = cursor::get_pos().expect("Getting the cursor position failed");
            CUR_Y = y - 3;
        }

        let (_, ny) = cursor::get_pos().expect("Getting the cursor position failed");
        cursor::set_pos(0, ny - 3).expect("Set failed again");
        PROGRESS_BEGAN = true;
    }

    // store cursor position, advance to new line
    let (cx, cy) = cursor::get_pos().expect("Getting the cursor position failed");

    // todo: print progress

    let mut prog_val = (_prog / 5) as usize;
    if prog_val > 0 && prog_val < 20 {
        prog_val += 1;
    }

    let progress_bar_compl = format!(
        "\x1b[38;2;0;180;0m{}\x1b[32m\x1b[0m",
        format!("{:█<prog_val$}", "")
    );
    let prog_val_e = 20 - prog_val;
    // let progress_bar_empty = format!(
    //     "\x1b[38;2;80;80;80m{}\x1b[32m\x1b[0m",
    //     format!("{:▒<prog_val_e$}", "")
    // );

    let progress_bar_empty = format!(
        "\x1b[38;2;80;80;80m{}\x1b[32m\x1b[0m",
        format!("{:█<prog_val_e$}", "")
    );
    let progress_bar = format!(" {}{} ", progress_bar_compl, progress_bar_empty);

    print!("{}", progress_bar);

    print!(" \x1b[0m{:3}%\x1b[0m", _prog);

    let ttl_width_msg = wnd_width - 29;
    // print title, advance to new line
    print!("\x1b[1m\x1b[38;2;180;180;0m");
    if LAST_TITLE_LEN <= title.len() {
        print!(" {:.ttl_width_msg$}\n", title);
    } else {
        print!(
            " {:.ttl_width_msg$}\n",
            format!("{:<LAST_TITLE_LEN$}", title)
        );
    }
    print!("\x1b[0m\x1b[38;2;160;160;160m");

    let wnd_width_msg = wnd_width - 3;

    // print message
    if LAST_MSG_LEN <= msg.len() {
        print!(" {:.wnd_width_msg$}\x1b[0m", msg);
    } else {
        print!(
            " {:.wnd_width_msg$}\x1b[0m",
            format!("{:<LAST_MSG_LEN$}", msg)
        );
    }

    LAST_MSG_LEN = msg.len(); // size is 25 + msg
    LAST_TITLE_LEN = title.len(); // std size of title len is 29 + title

    // construct string of CPU cores
    let offset: usize = 6 + title_size + msg_size;

    STATUS_LEN = cpuprogress::print_cpu_mem_progress(data, offset);

    cursor::set_pos(cx, cy).expect("Set failed again");
}

fn print_help_and_exit() {
    println!(
        "Unity Command tool v. 0.2 {} {}, built with {}",
        env!("BUILD_DATE"),
        env!("BUILD_TIME"),
        env!("RUSTC_VERSION"),
    );

    println!("Usage:");
    println!("\t'uc <command> <parameters> <flags>' sends command to default host editor");
    println!("\t'uc list commands' shows a list of all available commands");
    //println!("\t'uc help' shows help page");
    println!("");
    println!("While not required, you may opt to use promt extenders for your shell and use ucs tool along uc.");
    println!("XXX See README.md for more information.");
    println!("");
    println!("To download and update new version of uc, clone this Git: https://github.com/Luunoronti/uc");
    println!("You will need to install Rust to build uc.");
    println!(
        "Once cloned, build uc. To do so, change target to uc_rust and invoke 'Cargo build -r'."
    );

    println!();
    exit(0);
}
fn construct_command() -> String {
    let args: Vec<String> = env::args()
        .skip(1)
        .map(|arg: String| {
            if arg.contains(' ') {
                format!("\"{}\"", arg)
            } else {
                arg
            }
        })
        .collect();
    return args.join(" ");
}
fn send_command(command: &String, socket: &UdpSocket) {
    let cmd_bytes: &[u8] = command.as_bytes();
    let cmd_len: usize = cmd_bytes.len();
    let mut buffer: Vec<u8> = vec![0; cmd_len + 3];
    buffer[3..cmd_len + 3].copy_from_slice(cmd_bytes);
    buffer[0] = 0x02;
    buffer[1] = (cmd_len >> 8) as u8;
    buffer[2] = (cmd_len & 0xff) as u8;

    let ipendpoint: String =
        fs::read_to_string("editorconsole.cfg").expect("editor console cfg error");
    match socket.send_to(&buffer, ipendpoint) {
        Ok(_sent) => {}
        Err(_e) => {
            println!("[Error: {}]", _e);
            exit(-1);
        }
    };
}
