create database if not exists SystemEye_DB;
use SystemEye_DB;

-- Tabelle für Minuten-Daten
create table if not exists minute_data(
id int auto_increment primary key,
timestamp datetime,
name varchar(100),
hardware_type varchar(50),
min_value double,
max_value double,
avg_value double,
format varchar(10)
);

-- Tabelle für StundenDaten
create table if not exists hour_data(
id int auto_increment primary key,
timestamp datetime,
name varchar(100),
hardware_type varchar(50),
min_value double,
max_value double,
avg_value double,
format varchar(10)
);

