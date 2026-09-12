# ADR 0001：可設定的模型供應層

- 狀態：Accepted
- 日期：2026-09-12

## 背景

原實作雖以 `IChatClient` 呼叫模型，但 `Program.cs` 寫死 Ollama endpoint/model，四個 Agent 也直接使用 Ollama-specific options。更換成雲端 API 需要修改 composition root 與所有 Agent，無法安全比較地端、雲端或混合配置。

## 決策

- Core 新增 `IAgentModelResolver` 與 `IAgentModel`，由 `AgentExecutor` 依 agent name 解析模型。
- 使用 `AgentModels` configuration 建立具名 profile，包含 default profile 與 per-Agent override。
- Provider client 的建立及專屬 options 位於 `AgentLab.Infrastructure`。
- 第一版內建 `Ollama` 與 `OpenAICompatible` factories。
- Profile client lazy 建立並重用；resolver 不做無聲 fallback。
- API key 僅從指定的環境變數讀取，不存入 repository 設定。
- 新 provider 透過 `IAgentModelProviderFactory` 擴充，不修改 Agent 或 Core runtime。

## 結果

優點：

- 可以只改設定切換全部 Agent，或讓不同 Agent 使用不同 profile。
- Core 移除 Ollama package 與 vendor-specific options。
- Provider selection 可用 fake factories 做 deterministic test。
- 錯誤設定與缺少 credential 會產生明確錯誤。

代價：

- 新增 Infrastructure project、profile schema 與 factory lifecycle。
- 不同 provider 的 capability 仍可能不同；structured output、reasoning 與 tool support 必須在後續加入 capability validation，不能假設所有模型相同。
- `OpenAICompatible` 只適用相容 protocol；其他雲端服務仍需新增 adapter。

## 未採用方案

- **只替換全域 `IChatClient`**：簡單，但無法 per-Agent 選擇，也無法集中處理 provider-specific options。
- **在 `Program.cs` 使用 provider switch**：會讓 composition root 持續膨脹，難以獨立測試與擴充。
- **所有地端服務都改走 OpenAI-compatible protocol**：會失去 Ollama 原生 options，且不保證使用者的地端服務提供相容 endpoint。
