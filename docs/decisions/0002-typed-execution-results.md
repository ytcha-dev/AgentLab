# ADR 0002：Typed execution results 與 Workflow terminal status

- 狀態：Accepted
- 日期：2026-09-12
- 範圍：M0 Baseline 與契約的第一個 vertical slice

## 背景

原本 `AgentExecutor` 只回傳 provider 的 `ChatResponse`，四個 Agent 再各自取出文字。`AgentWorkflow` 最後只回傳 plan、implementation 與 review，因此呼叫端無法從結果判斷 run／step／attempt、實際模型、usage、duration 或執行失敗；Reviewer 最後仍拒絕時也沒有明確的 terminal status。

若沒有穩定的執行契約，後續 failure policy、retry、metrics、trace 與 eval 都只能依賴 log 或例外，難以做 deterministic test。

## 決策

1. 以 `RunContext` 識別一次 workflow run，並由它建立包含 `RunId`、`WorkflowId`、`StepId`、`Attempt` 的 `AgentExecutionContext`。
2. `AgentExecutor` 回傳 `AgentExecutionResult`，統一攜帶 step status、resolved model descriptor、token usage、duration、finish reason 與可選的 `FailureInfo`。
3. Provider 原始輸出只供 Agent 解析，不列入 API 序列化；Agent 以 `AgentStepResult<T>` 將 execution metadata 與 typed value 組合。
4. `IAgent<TInput,TOutput>` 接受明確 execution context，並回傳 `AgentStepResult<TOutput>`。
5. `WorkflowResult` 回傳 `RunContext`、terminal `WorkflowStatus`、目前 artifacts、全部 model-call steps 與 terminal failure。
6. Reviewer 的 `Approved=false` 代表模型呼叫成功、但 workflow 尚未符合品質條件；耗盡兩次 fix 後回傳 `NotApproved`，不偽裝成 `Succeeded` 或 provider failure。
7. API 將 enum 序列化為名稱，讓使用者可直接讀取 `Succeeded`、`NotApproved`、`Failed`、`Cancelled`。

## 邊界

本切片只建立契約與最小 terminal mapping。`OutputTruncated`、caller cancellation 與未分類 model exception 可以形成 typed failure；empty response、invalid JSON、timeout、provider error taxonomy、retry/backoff 與 structured-output repair 留給下一個 Failure normalization 切片。

一個 Agent 目前只包含一次 model call，所以 `AgentStepResult<T>` 對應一個 `AgentExecutionResult`。未來 Agent 若包含多個 model/tool calls，應新增 child execution collection，不把現有欄位改回非結構化文字。

## 替代方案

- **只強化 log，不改回傳型別**：無法讓 API、tests 與 eval 可靠判斷 terminal state，拒絕。
- **把 metadata 放在 ambient context 或 singleton collector**：增加隱性狀態與並行污染風險，拒絕。
- **將完整 `ChatResponse` 暴露到 Workflow/API**：會洩漏 provider-specific/raw metadata，且讓 Core 契約依賴外部形狀，拒絕。
- **本切片同時完成完整 retry policy**：範圍過大且難以區分契約與策略錯誤，延至下一切片。

## 影響

- `/agent/plan`、`/agent/develop`、`/agent/review` 的成功回應改為包含 run 與 step wrapper；`/agent/run` 增加 status、steps 與 failure，屬於有意識的 API contract change。
- 所有 step 可用相同 run id 串聯，並辨識 review/fix attempt。
- Raw model output 不會出現在 `WorkflowResult.Steps`；plan、implementation、review 仍由各自 typed 欄位回傳。
- 下一切片可在不再改變外層 API 形狀的前提下細化 `FailureInfo` 與 retry policy。

## 驗證

以 queue-based fake `IChatClient` 驗證：第一次核准、拒絕後 fix 再核准、三次 review 均拒絕、provider exception 與 caller cancellation。測試不依賴 Ollama 或雲端 credential。
