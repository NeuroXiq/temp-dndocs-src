PRAGMA journal_mode=WAL;
PRAGMA page_size=4096;

BEGIN TRANSACTION;

create table app_log
(
    id integer primary key autoincrement,
    [message] text,
    category_name,
    log_level_id int,
    event_id int,
    event_name text,
    [date] text
);

create table http_log
(
id integer primary key autoincrement,
[date] text,
[time] text,
client_ip text,
client_port int,
method text,
uri_path text,
uri_query text,
response_status int,
bytes_send int,
bytes_received int,
time_taken_ms int,
host text,
user_agent text,
referer text
);

create table resource_monitor_utilization
(
id integer primary key,
cpu_used_percentage integer,
memory_used_in_bytes integer,
memory_used_percentage real,
date_time text
);


COMMIT;