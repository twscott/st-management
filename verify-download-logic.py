import mysql.connector
from datetime import datetime

conn = mysql.connector.connect(
    host='127.0.0.1', 
    port=3306, 
    user='root', 
    password='', 
    database='sst'
)
cursor = conn.cursor()
cursor.execute('SELECT recDate, lastDate FROM investbase LIMIT 1')
result = cursor.fetchone()

current_hour = datetime.now().hour
current_time = datetime.now().strftime("%Y-%m-%d %H:%M")

print('=' * 60)
print('📊 修改后的下载逻辑验证')
print('=' * 60)
print(f'当前时间: {current_time}, Hour={current_hour}')
print()
print(f'investbase 状态:')
print(f'  recDate:  {result[0]}')
print(f'  lastDate: {result[1]}')
print()

if current_hour >= 15:
    print(f'✅ 判断: 当前时间 >= 15:00')
    print(f'✅ 应该下载: investbase.recDate = {result[0]}')
else:
    print(f'⏰ 判断: 当前时间 < 15:00')
    print(f'⏰ 应该下载: investbase.lastDate = {result[1]}')

print('=' * 60)
conn.close()
