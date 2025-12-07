# GoodInfo 19個Link配置清單

## Link對應表

| # | Link名稱 | LinkLabel | Click Event | 狀態 |
|---|---------|-----------|-------------|------|
| 1 | 券資比 | linkLabel4 | linkLabel4_LinkClicked | ✅ 已分析 |
| 2 | 周轉率 | linkLabel9 | linkLabel9_LinkClicked | ✅ 已完成 |
| 3 | 超布林上軌 | linkLabel3 | linkLabel3_LinkClicked | ⏳ 待分析 |
| 4 | 外資連買連賣轉折 | linkLabel7 | linkLabel7_LinkClicked | ⏳ 待分析 |
| 5 | 投信連買連賣轉折 | linkLabel8 | linkLabel8_LinkClicked | ⏳ 待分析 |
| 6 | MACD<0. OSC 負轉正 | linkLabel11 | ? | ⚠️ 需確認 |
| 7 | 月季黃金 | 月季黃金 | linkLabel14_LinkClicked | ⏳ 待分析 |
| 8 | 外資連買 | linkLabel32 | linkLabel32_LinkClicked | ⏳ 待分析 |
| 9 | 外資連賣 | linkLabel33 | linkLabel33_LinkClicked | ⏳ 待分析 |
| 10 | 投信連買 | linkLabel29 | linkLabel29_LinkClicked | ⏳ 待分析 |
| 11 | 投信連賣 | linkLabel28 | linkLabel28_LinkClicked | ⏳ 待分析 |
| 12 | 外資、投信同步買超 | linkLabel30 | linkLabel30_LinkClicked | ⏳ 待分析 |
| 13 | 外資、投信同步賣超 | linkLabel31 | linkLabel31_LinkClicked | ⏳ 待分析 |
| 14 | 五年新高 | 五年新高 | linkLabel23_LinkClicked | ⏳ 待分析 |
| 15 | 歷史成交量 | 歷史成交量 | linkLabel1_LinkClicked_1 | ⏳ 待分析 |
| 16 | EPS創新高 | linkLabel38 | ? | ⚠️ 需確認 |
| 17 | 季營收創高 | linkLabel44 | ? | ⚠️ 需確認 |
| 18 | 財報評分 | linkLabel34 | ? | ⚠️ 需確認 |
| 19 | MACD>0 | ? | ? | ❌ 未找到 |

## 注意事項

1. **已完成**: 周轉率 (linkLabel9)
2. **進行中**: 券資比 (linkLabel4)
3. **部分Link沒有LinkClicked事件**: 
   - linkLabel38 (EPS創新高)
   - linkLabel44 (季營收創高)
   - linkLabel34 (財報評分)
   - 可能是顯示用，不是下載用

## 下一步行動

需要確認：
1. EPS創新高、季營收創高、財報評分 是否有實際下載功能？
2. MACD>0 是否存在？還是跟 OSC 合併了？
3. MACD<0. OSC 負轉正 的 Click event 是什麼？
