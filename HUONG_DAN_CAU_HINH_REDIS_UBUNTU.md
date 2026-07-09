# Hướng Dẫn Cấu Hình Redis Trên 3 Ubuntu VM

Tài liệu này hướng dẫn cấu hình Redis từ đầu với 3 máy ảo Ubuntu mới tạo, phục vụ hệ thống Redis Demo:

```text
VM1: 192.168.203.128  app-server-sentinel
VM2: 192.168.203.129  redis-replica-1
VM3: 192.168.203.130  redis-replica-2
```

Mô hình sau khi hoàn thành:

```text
Redis HA 6379:
  - 3 Redis node
  - 1 master do Sentinel quyết định
  - 2 replica
  - maxmemory-policy noeviction

Sentinel 26379:
  - 3 Sentinel node
  - quorum 2
  - service name: mymaster

Redis Cache 6380:
  - chạy standalone trên VM1
  - dùng cho product cache
  - maxmemory 128mb
  - maxmemory-policy allkeys-lru

Redis Persistence Test 6381:
  - chạy standalone trên VM1
  - dùng riêng để test RDB/AOF
  - appendonly yes
  - save 900 1 / 300 10 / 60 10000
```

Mật khẩu Redis dùng trong bài:

```text
RedisDemo@123
```

## 1. Kiểm Tra Mạng VM

Trên từng VM chạy:

```bash
ip a
```

Kết quả cần có IP cùng dải:

```text
VM1: 192.168.203.128
VM2: 192.168.203.129
VM3: 192.168.203.130
```

Kiểm tra ping qua lại:

```bash
ping -c 4 192.168.203.128
ping -c 4 192.168.203.129
ping -c 4 192.168.203.130
```

Nếu ping không được, kiểm tra lại VMware Network Adapter, VMnet8/NAT hoặc firewall của Ubuntu.

## 2. Cài Redis Trên Cả 3 VM

Làm trên cả VM1, VM2, VM3:

```bash
sudo apt update
sudo apt install redis-server -y
redis-server --version
```

Bật service:

```bash
sudo systemctl enable redis-server
sudo systemctl start redis-server
sudo systemctl status redis-server --no-pager -l
```

## 3. Cấu Hình Redis HA Port 6379

Làm trên cả 3 VM:

```bash
sudo nano /etc/redis/redis.conf
```

Đặt các dòng chính:

```conf
bind 0.0.0.0
protected-mode yes
port 6379
requirepass RedisDemo@123
masterauth RedisDemo@123

supervised systemd

save 900 1
save 300 10
save 60 10000

appendonly yes
appendfsync everysec

maxmemory 0
maxmemory-policy noeviction
```

Ý nghĩa:

- `bind 0.0.0.0`: cho phép máy khác trong mạng kết nối.
- `requirepass`: mật khẩu client dùng để truy cập Redis.
- `masterauth`: mật khẩu replica dùng để kết nối master.
- `maxmemory-policy noeviction`: Redis HA không tự đẩy mất session/cart/stream/ranking khi thiếu bộ nhớ.

## 4. Cấu Hình Replication

Ban đầu chọn VM1 `.128` làm master, VM2 `.129` và VM3 `.130` làm replica.

Trên VM1:

```bash
sudo nano /etc/redis/redis.conf
```

Đảm bảo không có dòng:

```conf
replicaof ...
slaveof ...
```

Trên VM2:

```bash
sudo nano /etc/redis/redis.conf
```

Thêm:

```conf
replicaof 192.168.203.128 6379
```

Trên VM3:

```bash
sudo nano /etc/redis/redis.conf
```

Thêm:

```conf
replicaof 192.168.203.128 6379
```

Restart Redis theo thứ tự:

```bash
sudo systemctl restart redis-server
```

## 5. Mở Firewall Cho Redis 6379 Và Sentinel 26379

Làm trên cả 3 VM:

```bash
sudo ufw allow from 192.168.203.0/24 to any port 6379 proto tcp
sudo ufw allow from 192.168.203.0/24 to any port 26379 proto tcp
sudo ufw allow from 192.168.179.0/24 to any port 6379 proto tcp
sudo ufw allow from 192.168.179.0/24 to any port 26379 proto tcp
```

Nếu UFW đang tắt, lệnh `ufw reload` có thể báo:

```text
Firewall not enabled (skipping reload)
```

Không sao.

## 6. Kiểm Tra Replication

Trên VM1:

```bash
redis-cli -a 'RedisDemo@123' INFO replication
```

Kỳ vọng:

```text
role:master
connected_slaves:2
slave0:ip=192.168.203.129,port=6379,state=online
slave1:ip=192.168.203.130,port=6379,state=online
```

Trên VM2 và VM3:

```bash
redis-cli -a 'RedisDemo@123' INFO replication
```

Kỳ vọng:

```text
role:slave
master_host:192.168.203.128
master_link_status:up
```

Test dữ liệu:

```bash
redis-cli -a 'RedisDemo@123' SET replication:test "hello from primary"
redis-cli -a 'RedisDemo@123' GET replication:test
```

Trên replica:

```bash
redis-cli -a 'RedisDemo@123' GET replication:test
```

Kết quả:

```text
"hello from primary"
```

## 7. Cấu Hình Redis Sentinel Trên Cả 3 VM

Tạo file Sentinel:

```bash
sudo nano /etc/redis/sentinel.conf
```

Dán nội dung sau trên cả 3 VM:

```conf
port 26379
bind 0.0.0.0
protected-mode no

dir /var/lib/redis

sentinel monitor mymaster 192.168.203.128 6379 2
sentinel auth-pass mymaster RedisDemo@123
sentinel down-after-milliseconds mymaster 5000
sentinel failover-timeout mymaster 10000
sentinel parallel-syncs mymaster 1

logfile /var/log/redis/redis-sentinel.log
```

Lưu ý:

- `mymaster` là service name backend sẽ dùng.
- `2` là quorum, nghĩa là cần ít nhất 2 Sentinel đồng ý master down.
- Nếu sau failover master đổi sang IP khác, Sentinel tự cập nhật file `sentinel.conf`.

Tạo thư mục nếu cần:

```bash
sudo mkdir -p /var/lib/redis
sudo chown redis:redis /var/lib/redis
sudo touch /var/log/redis/redis-sentinel.log
sudo chown redis:redis /var/log/redis/redis-sentinel.log
```

Tạo service:

```bash
sudo nano /etc/systemd/system/redis-sentinel.service
```

Dán:

```ini
[Unit]
Description=Redis Sentinel
After=network.target

[Service]
Type=simple
ExecStart=/usr/bin/redis-server /etc/redis/sentinel.conf --sentinel
Restart=always
User=redis
Group=redis

[Install]
WantedBy=multi-user.target
```

Start Sentinel:

```bash
sudo systemctl daemon-reload
sudo systemctl enable redis-sentinel
sudo systemctl start redis-sentinel
sudo systemctl status redis-sentinel --no-pager -l
```

Nếu Sentinel lỗi, xem log:

```bash
sudo journalctl -u redis-sentinel -n 80 --no-pager
```

## 8. Kiểm Tra Sentinel

Chạy trên một VM bất kỳ:

```bash
redis-cli -p 26379 SENTINEL get-master-addr-by-name mymaster
```

Kỳ vọng ban đầu:

```text
1) "192.168.203.128"
2) "6379"
```

Kiểm tra replica:

```bash
redis-cli -p 26379 SENTINEL replicas mymaster
```

Kiểm tra Sentinel khác:

```bash
redis-cli -p 26379 SENTINEL sentinels mymaster
```

Kiểm tra master info:

```bash
redis-cli -p 26379 SENTINEL master mymaster
```

Kỳ vọng:

```text
num-slaves: 2
num-other-sentinels: 2
quorum: 2
```

## 9. Test Failover

Có hai cách.

Cách 1, failover thủ công:

```bash
redis-cli -p 26379 SENTINEL failover mymaster
```

Sau đó kiểm tra:

```bash
redis-cli -p 26379 SENTINEL get-master-addr-by-name mymaster
```

Master có thể đổi từ `.128` sang `.129` hoặc `.130`.

Cách 2, tắt master hiện tại:

```bash
sudo systemctl stop redis-server
```

Chờ khoảng 5-15 giây rồi kiểm tra:

```bash
redis-cli -p 26379 SENTINEL get-master-addr-by-name mymaster
```

Khi bật lại node cũ:

```bash
sudo systemctl start redis-server
```

Node cũ thường quay lại làm replica, không tự giành lại master ngay.

## 10. Chuẩn Hóa Policy noeviction Cho Cả 3 Node 6379

Sau failover, master có thể là `.130`, `.129` hoặc `.128`. Vì vậy policy phải đúng trên cả 3 node.

Trên từng VM:

```bash
redis-cli -a 'RedisDemo@123' CONFIG GET maxmemory-policy
redis-cli -a 'RedisDemo@123' CONFIG GET maxmemory
```

Kỳ vọng:

```text
maxmemory-policy
noeviction

maxmemory
0
```

Nếu chưa đúng, sửa `/etc/redis/redis.conf`:

```conf
maxmemory 0
maxmemory-policy noeviction
```

Restart replica trước, master sau:

```bash
sudo systemctl restart redis-server
```

## 11. Tạo Redis Cache Riêng Port 6380 Trên VM1

Redis Cache 6380 dùng cho:

```text
cache:products
cache:product:{id}
lock:cache:products:rebuild
lock:cache:product:{id}:rebuild
metrics:cache:products:*
```

Tạo file:

```bash
sudo nano /etc/redis/redis-cache-6380.conf
```

Dán:

```conf
bind 0.0.0.0
protected-mode yes
port 6380
tcp-backlog 511
timeout 0
tcp-keepalive 300

supervised no
pidfile /run/redis/redis-cache-6380.pid
loglevel notice
logfile /var/log/redis/redis-cache-6380.log

databases 16
always-show-logo yes

save ""
appendonly no

dir /var/lib/redis-6380

requirepass RedisDemo@123

maxmemory 128mb
maxmemory-policy allkeys-lru
```

Tạo thư mục và quyền:

```bash
sudo mkdir -p /var/lib/redis-6380
sudo chown redis:redis /var/lib/redis-6380
sudo chmod 750 /var/lib/redis-6380

sudo touch /var/log/redis/redis-cache-6380.log
sudo chown redis:redis /var/log/redis/redis-cache-6380.log
sudo chmod 640 /var/log/redis/redis-cache-6380.log

sudo chown root:redis /etc/redis/redis-cache-6380.conf
sudo chmod 640 /etc/redis/redis-cache-6380.conf
```

Kiểm tra Redis user đọc được config:

```bash
sudo -u redis test -r /etc/redis/redis-cache-6380.conf && echo "redis user can read config" || echo "redis user cannot read config"
```

Nếu không đọc được, sửa:

```bash
sudo chown root:redis /etc/redis/redis-cache-6380.conf
sudo chmod 640 /etc/redis/redis-cache-6380.conf
```

Tạo service:

```bash
sudo nano /etc/systemd/system/redis-cache-6380.service
```

Dán:

```ini
[Unit]
Description=Redis Cache Instance 6380
After=network.target

[Service]
Type=simple
ExecStart=/usr/bin/redis-server /etc/redis/redis-cache-6380.conf
ExecStop=/usr/bin/redis-cli -p 6380 -a RedisDemo@123 shutdown
Restart=always
User=redis
Group=redis
RuntimeDirectory=redis
RuntimeDirectoryMode=2755

[Install]
WantedBy=multi-user.target
```

Mở firewall:

```bash
sudo ufw allow from 192.168.203.0/24 to any port 6380 proto tcp
sudo ufw allow from 192.168.179.0/24 to any port 6380 proto tcp
```

Start:

```bash
sudo systemctl daemon-reload
sudo systemctl enable redis-cache-6380
sudo systemctl start redis-cache-6380
sudo systemctl status redis-cache-6380 --no-pager -l
```

Kiểm tra:

```bash
redis-cli -p 6380 -a 'RedisDemo@123' PING
redis-cli -p 6380 -a 'RedisDemo@123' INFO replication
redis-cli -p 6380 -a 'RedisDemo@123' CONFIG GET maxmemory
redis-cli -p 6380 -a 'RedisDemo@123' CONFIG GET maxmemory-policy
```

Kỳ vọng:

```text
PONG
role:master
connected_slaves:0
maxmemory
134217728
maxmemory-policy
allkeys-lru
```

## 12. Tạo Redis Persistence Test Riêng Port 6381 Trên VM1

Redis 6381 dùng riêng để chứng minh RDB/AOF, không Sentinel, không replica.

Tạo file:

```bash
sudo nano /etc/redis/redis-persistence-6381.conf
```

Dán:

```conf
bind 0.0.0.0
protected-mode yes
port 6381
tcp-backlog 511
timeout 0
tcp-keepalive 300

supervised no
pidfile /run/redis/redis-persistence-6381.pid
loglevel notice
logfile /var/log/redis/redis-persistence-6381.log

databases 16
always-show-logo yes

save 900 1
save 300 10
save 60 10000

appendonly yes
appendfsync everysec

dir /var/lib/redis-6381

requirepass RedisDemo@123

maxmemory 0
maxmemory-policy noeviction
```

Tạo thư mục và quyền:

```bash
sudo mkdir -p /var/lib/redis-6381
sudo chown redis:redis /var/lib/redis-6381
sudo chmod 750 /var/lib/redis-6381

sudo touch /var/log/redis/redis-persistence-6381.log
sudo chown redis:redis /var/log/redis/redis-persistence-6381.log
sudo chmod 640 /var/log/redis/redis-persistence-6381.log

sudo chown root:redis /etc/redis/redis-persistence-6381.conf
sudo chmod 640 /etc/redis/redis-persistence-6381.conf
```

Tạo service:

```bash
sudo nano /etc/systemd/system/redis-persistence-6381.service
```

Dán:

```ini
[Unit]
Description=Redis Persistence Test Instance 6381
After=network.target

[Service]
Type=simple
ExecStart=/usr/bin/redis-server /etc/redis/redis-persistence-6381.conf
ExecStop=/usr/bin/redis-cli -p 6381 -a RedisDemo@123 shutdown
Restart=always
User=redis
Group=redis
RuntimeDirectory=redis
RuntimeDirectoryMode=2755

[Install]
WantedBy=multi-user.target
```

Mở firewall:

```bash
sudo ufw allow from 192.168.203.0/24 to any port 6381 proto tcp
sudo ufw allow from 192.168.179.0/24 to any port 6381 proto tcp
```

Start:

```bash
sudo systemctl daemon-reload
sudo systemctl enable redis-persistence-6381
sudo systemctl start redis-persistence-6381
sudo systemctl status redis-persistence-6381 --no-pager -l
```

Kiểm tra:

```bash
redis-cli -p 6381 -a 'RedisDemo@123' PING
redis-cli -p 6381 -a 'RedisDemo@123' INFO replication
redis-cli -p 6381 -a 'RedisDemo@123' CONFIG GET appendonly
redis-cli -p 6381 -a 'RedisDemo@123' CONFIG GET save
redis-cli -p 6381 -a 'RedisDemo@123' CONFIG GET maxmemory-policy
```

Kỳ vọng:

```text
PONG
role:master
connected_slaves:0
appendonly
yes
save
900 1 300 10 60 10000
maxmemory-policy
noeviction
```

## 13. Test Persistence Trên Redis 6381

Xóa key cũ nếu có:

```bash
redis-cli -p 6381 -a 'RedisDemo@123' DEL \
persistence:test:cache \
persistence:test:session \
persistence:test:cart \
persistence:test:ranking \
persistence:test:stream
```

Tạo dữ liệu:

```bash
redis-cli -p 6381 -a 'RedisDemo@123'
```

Trong redis-cli:

```redis
SET persistence:test:cache "cache ok"
HSET persistence:test:session userId 1 username persistence_user createdAt "2026-07-08"
HSET persistence:test:cart 1 2 2 1
ZADD persistence:test:ranking 10 product:1 7 product:2
XADD persistence:test:stream * event persistence-test createdAt "2026-07-08"
DBSIZE
BGSAVE
exit
```

Kiểm tra trước restart:

```bash
redis-cli -p 6381 -a 'RedisDemo@123' EXISTS \
persistence:test:cache \
persistence:test:session \
persistence:test:cart \
persistence:test:ranking \
persistence:test:stream
```

Kỳ vọng:

```text
(integer) 5
```

Kiểm tra RDB/AOF:

```bash
redis-cli -p 6381 -a 'RedisDemo@123' INFO persistence | grep -E 'rdb_last_bgsave_status|aof_enabled|aof_last_write_status'
```

Kỳ vọng:

```text
rdb_last_bgsave_status:ok
aof_enabled:1
aof_last_write_status:ok
```

Restart riêng instance 6381:

```bash
sudo systemctl restart redis-persistence-6381
sleep 2
sudo systemctl status redis-persistence-6381 --no-pager -l
```

Kiểm tra sau restart:

```bash
redis-cli -p 6381 -a 'RedisDemo@123' EXISTS \
persistence:test:cache \
persistence:test:session \
persistence:test:cart \
persistence:test:ranking \
persistence:test:stream
```

Kỳ vọng:

```text
(integer) 5
```

Kiểm tra nội dung:

```bash
redis-cli -p 6381 -a 'RedisDemo@123' GET persistence:test:cache
redis-cli -p 6381 -a 'RedisDemo@123' HGETALL persistence:test:session
redis-cli -p 6381 -a 'RedisDemo@123' HGETALL persistence:test:cart
redis-cli -p 6381 -a 'RedisDemo@123' ZRANGE persistence:test:ranking 0 -1 WITHSCORES
redis-cli -p 6381 -a 'RedisDemo@123' XRANGE persistence:test:stream - +
```

## 14. Cấu Hình Backend Sau Khi Redis Xong

Trong `backend/appsettings.json`:

```json
"Redis": {
  "Host": "192.168.203.128",
  "Port": 6379,
  "Password": "RedisDemo@123",
  "Database": 0,
  "Sentinel": {
    "Enabled": true,
    "ServiceName": "mymaster",
    "Endpoints": [
      { "Host": "192.168.203.128", "Port": 26379 },
      { "Host": "192.168.203.129", "Port": 26379 },
      { "Host": "192.168.203.130", "Port": 26379 }
    ]
  },
  "Cache": {
    "Enabled": true,
    "Host": "192.168.203.128",
    "Port": 6380,
    "Password": "RedisDemo@123",
    "Database": 0
  },
  "Persistence": {
    "Enabled": true,
    "Host": "192.168.203.128",
    "Port": 6381,
    "Password": "RedisDemo@123",
    "Database": 0
  }
}
```

Ý nghĩa:

```text
Session / Cart / Rate Limit / Ranking / Stream / PubSub
-> Redis HA 6379 qua Sentinel

Product Cache
-> Redis Cache 6380

Persistence Test
-> Redis Persistence 6381
```

## 15. Kiểm Tra Runtime Từ Backend

Chạy backend:

```bash
cd backend
dotnet run --launch-profile http
```

Gọi API:

```text
GET http://127.0.0.1:5000/api/redis/ping
GET http://127.0.0.1:5000/api/redis/infrastructure
GET http://127.0.0.1:5000/api/redis/persistence/check
GET http://127.0.0.1:5000/api/products
```

Kết quả runtime tốt:

```text
Current master: 192.168.203.130:6379 hoặc master hiện tại do Sentinel trả về

6379 policies:
192.168.203.128:6379 -> noeviction
192.168.203.129:6379 -> noeviction
192.168.203.130:6379 -> noeviction

6380:
endpoint  -> 192.168.203.128:6380
maxmemory -> 134217728
policy    -> allkeys-lru

6381:
endpoint   -> 192.168.203.128:6381
appendonly -> yes
save       -> 900 1 300 10 60 10000
```

## 16. Lỗi Thường Gặp

### Redis service không đọc được config

Triệu chứng:

```text
Fatal error, can't open config file
```

Kiểm tra:

```bash
ls -l /etc/redis/redis-cache-6380.conf
sudo -u redis test -r /etc/redis/redis-cache-6380.conf && echo ok || echo fail
```

Sửa:

```bash
sudo chown root:redis /etc/redis/redis-cache-6380.conf
sudo chmod 640 /etc/redis/redis-cache-6380.conf
```

### Sai thư mục log hoặc dir

Sửa:

```bash
sudo mkdir -p /var/lib/redis-6380 /var/lib/redis-6381
sudo chown redis:redis /var/lib/redis-6380 /var/lib/redis-6381

sudo touch /var/log/redis/redis-cache-6380.log
sudo touch /var/log/redis/redis-persistence-6381.log
sudo chown redis:redis /var/log/redis/redis-cache-6380.log
sudo chown redis:redis /var/log/redis/redis-persistence-6381.log
```

### Sentinel không chạy

Xem log:

```bash
sudo journalctl -u redis-sentinel -n 80 --no-pager
```

Lỗi thường gặp:

```text
Can't chdir to '/var/libs/redis'
```

Sửa lại dòng `dir` trong `/etc/redis/sentinel.conf`:

```conf
dir /var/lib/redis
```

### Replica không sync master

Kiểm tra:

```bash
redis-cli -a 'RedisDemo@123' INFO replication
ping -c 4 <master-ip>
sudo ufw status
```

Các nguyên nhân thường gặp:

- Sai IP master.
- Port 6379 chưa mở.
- `masterauth` thiếu hoặc sai.
- VM khác network mode.

## 17. Kết Quả Cuối Cần Đạt

```text
Redis HA:
  3 node online
  1 master
  2 replica
  3 Sentinel
  quorum 2
  failover OK
  policy cả 3 node: noeviction

Redis Cache 6380:
  PONG
  role:master
  connected_slaves:0
  maxmemory: 134217728
  maxmemory-policy: allkeys-lru

Redis Persistence 6381:
  PONG
  role:master
  connected_slaves:0
  appendonly: yes
  save: 900 1 300 10 60 10000
  persistence test: 5/5 PASS sau restart

Backend:
  Sentinel ping OK
  Product cache source REDIS sau lần gọi thứ hai
  Persistence check pass=true
  Dashboard infrastructure hiển thị policy/maxmemory/AOF/RDB
```
