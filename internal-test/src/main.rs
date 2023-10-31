//use winapi::um::winuser::{EnumWindows, GetWindowTextW, HWND, LPARAM, BOOL};
use windows::Win32::Foundation::{HWND, BOOL, TRUE, FALSE, LPARAM, WPARAM};
use windows::Win32::UI::WindowsAndMessaging::EnumWindows;
use windows::Win32::UI::WindowsAndMessaging::GetWindowTextW;

use std::os::raw::c_int;

fn main() {
    let ew = enumerate_windows();
    for name in &ew
        {
            println!("{}", name);
        }
    println!("Hello, world!");
}


fn enumerate_windows() -> Vec<String> {
    let mut windows = Vec::<String>::new();
    unsafe {
        EnumWindows(Some(enum_windows_callback), &mut windows as *mut Vec<String> as isize);
    }
    windows
}

unsafe extern "system" fn enum_windows_callback(hwnd: HWND, lparam: LPARAM) -> BOOL {
    let mut buffer = [0u16; 512];
    let len = GetWindowTextW(hwnd, buffer.as_mut_ptr(), buffer.len() as c_int);
    if len > 0 {
        let title = String::from_utf16_lossy(&buffer[..len as usize]);
        let windows = &mut *(lparam as *mut Vec<String>);
        windows.push(title);
    }
    TRUE
}