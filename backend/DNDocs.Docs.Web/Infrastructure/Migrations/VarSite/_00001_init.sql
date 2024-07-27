PRAGMA journal_mode=WAL;
PRAGMA page_size=4096;

BEGIN TRANSACTION;

create table shared_site_item
(
id integer primary key autoincrement,
[path] text,
byte_data BLOB,
sha_256 text
);

create table public_html
(
id integer primary key autoincrement,
[path] text,
byte_data BLOB
);

create table sitemap
(
id integer primary key autoincrement,
sitemap_name text,
created_on text,
updated_on text,
byte_data blob
);

create table project_sitemap
(
id integer primary key,
sitemap_id int,
project_id int
);

COMMIT;