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
static mut CUR_X: i32 = 0;
static mut CUR_Y: i32 = 0;

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
            Err(_) => exit(-1),
        };

        let data = &buffer[..size];
        process_resp(&data);
    }
}

fn process_resp(data: &[u8]) {
    let code = data[0];
    unsafe {
        match code {
            0x01 => exit(0), // command completed
            0xF0 => {
                hide_progress(); // make sure we hide progress. it may show later, but we must hide it for now
                print!(
                    "{}",
                    std::str::from_utf8(&data[1..(data.len() - 1)]).unwrap()
                )
            } // text message
            0x30 => {}       // ping, do nothing
            0x11 => hide_progress(), // hide progress
            0x10 => show_progress(data),
            _ => println!("{}", code),
        }
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

    print!("{}\n{}", format!("{:<ttl$}", ""), format!("{:<msl$}", ""));
    cursor::set_pos(CUR_X, CUR_Y).expect("Set failed again");
}
unsafe fn show_progress(data: &[u8]) {
    let _prog = data[1];
    let _cpu = data[2];
    let _mem = data[3];
    let title_size: usize = data[4] as usize;

    let title = std::str::from_utf8(&data[6..(6 + title_size)]).unwrap();
    let msg = std::str::from_utf8(&data[6 + title_size..data.len()]).unwrap();

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
            println!();
            println!();
            //println!();
            let (_, y) = cursor::get_pos().expect("Getting the cursor position failed");
            CUR_Y = y - 2;
        } else {
            println!();
            //println!();
            let (_, y) = cursor::get_pos().expect("Getting the cursor position failed");
            CUR_Y = y - 1;
        }

        let (_, ny) = cursor::get_pos().expect("Getting the cursor position failed");
        cursor::set_pos(0, ny - 1).expect("Set failed again");
        PROGRESS_BEGAN = true;
    }

    // store cursor position, advance to new line
    let (cx, cy) = cursor::get_pos().expect("Getting the cursor position failed");

    // todo: print progress
    let mut prog_val = (_prog / 5) as usize;
    if prog_val > 0 && prog_val < 20 {
        prog_val += 1;
    }

    let progress_bar = format!(
        "\x1b[33m[\x1b[32m{}\x1b[33m]\x1b[32m\x1b[0m",
        format!("{:-<20}", format!("{:█<prog_val$}", ""))
    );

    print!("{}", progress_bar);

    print!(" \x1b[32m{:3}%\x1b[0m", _prog);

    let ttl_width_msg = wnd_width - 29;
    // print title, advance to new line
    print!("\x1b[1m\x1b[93m");
    if LAST_TITLE_LEN <= title.len() {
        print!(" {:.ttl_width_msg$}\n", title);
    } else {
        print!(" {:.ttl_width_msg$}\n", format!("{:<LAST_TITLE_LEN$}", title));
    }
    print!("\x1b[0m");

    // print CPU and RAM
    let mut cpu_flag = "";
    if _cpu < 40 {
        cpu_flag = "\x1b[32m";
    } else if _cpu < 80 {
        cpu_flag = "\x1b[33m";
    } else if _cpu < 40 {
        cpu_flag = "\x1b[31m";
    }
    let mut mem_flag = "";
    if _mem < 40 {
        mem_flag = "\x1b[32m";
    } else if _mem < 80 {
        mem_flag = "\x1b[33m";
    } else if _mem < 40 {
        mem_flag = "\x1b[31m";
    }
    print!(
        "  CPU:{}{:3}%\x1b[0m  RAM:{}{:3}%\x1b[0m  ",
        cpu_flag, _cpu, mem_flag, _mem
    );

    print!("  ");

    let wnd_width_msg = wnd_width - 26;

    // print message
    if LAST_MSG_LEN <= msg.len() {
        print!("{:.wnd_width_msg$}", msg);
    } else {
        print!("{:.wnd_width_msg$}\n", format!("{:<LAST_MSG_LEN$}", msg));
    }

    LAST_MSG_LEN = msg.len(); // size is 25 + msg
    LAST_TITLE_LEN = title.len(); // std size of title len is 29 + title

    cursor::set_pos(cx, cy).expect("Set failed again");
}

fn print_help_and_exit() {
    println!("Unity Command tool v. 0.2");
    println!("Usage:");
    println!("\t'uc <command> <parameters> <flags>' sends command to default host editor");
    println!("\t'uc list commands' shows a list of all available commands");
    println!("\t'uc help' shows help page");
    println!("\t'uc selfupdate' to download and update uc");
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
