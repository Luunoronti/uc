use std::fs;
use std::net::UdpSocket;
use std::path::Path;
use std::time::Duration;

fn main() {
    if Path::new("editorconsolestatus.cfg").is_file() == false {
        return;
    }
    //println!("Creating socket!");
    let socket = UdpSocket::bind("0.0.0.0:0").expect("couldn't bind to address");

    socket
        .set_read_timeout(Some(Duration::from_millis(2000)))
        .expect("Could not set a read timeout");

    let mut _rcv_buff = [0; 1024]; // max size of message
                                   // we may decide later to change this

    // we must send anything, but we do not care about contens
    let contents = fs::read_to_string("editorconsolestatus.cfg").expect("[Error]");

    socket
        .send_to(&[0; 1], contents)
        .expect("couldn't send data");
    //println!("Data sent. Now will await receive!");

    match socket.recv(&mut _rcv_buff) {
        Ok(received) => {
            let my_string = std::str::from_utf8(&_rcv_buff[..received]).unwrap();
            //      println!("received {received} bytes {:?}", &_rcv_buff[..received]);
            println!("{}", my_string);
        }
        //Err(e) => println!("[Error]: {e:?}"),
        Err(_e) => println!("[Error]"),
    }
}
