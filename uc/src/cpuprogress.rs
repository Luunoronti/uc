
pub fn print_cpu_mem_progress(data: &[u8], _offset: usize) -> usize
{
    let mut offset = _offset;
    let _cpu = data[2];
    let _mem = data[3];

    let cores_count = (data[offset]) as usize;
    offset += 1;
    let mut cores_string = String::new();

    for _i in 0..cores_count {
        let s1 = cores_string;
        let s2 = get_cpu_core_mark(data[offset]);
        let s3 = s1 + &s2;
        cores_string = s3;
        offset += 1;
    }

    let cpucores = format!("{}", cores_string);

    // print CPU and RAM status
    let cpu = format!(
        "\x1b[38;2;{};{};0m{:3}%\x1b[0m",
        super::tools::get_red(_cpu),
        super::tools::get_green(_cpu),
        _cpu
    );
    let mem = format!(
        "\x1b[38;2;{};{};0m{:3}%\x1b[0m",
        super::tools::get_red(_mem),
        super::tools::get_green(_mem),
        _mem
    );

    let full_str = format!("\n CPU:{} {} \x1b[0m RAM:{}  ", cpu, cpucores, mem);
    print!("{}", full_str);
    full_str.len()
}

fn get_cpu_core_mark(value: u8) -> String {
    return String::from(format!(
        "\x1b[38;2;{};{};0m■\x1b[0m",
        super::tools::get_red(value),
        super::tools::get_green(value)
    ));
}