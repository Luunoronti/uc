
pub fn get_red(value: u8) -> i32 {
    (255.0 * ((value as f32) / 100.0)) as i32
}

pub fn get_green(value: u8) -> i32 {
    (255.0 - (255.0 * ((value as f32) / 100.0))) as i32
}
