# AgentLab 開發藍圖

> 狀態：可執行草案 1.0
> 日期：2026-09-12
> 適用範圍：目前的 `AgentLab` solution 與後續實驗
> 文件目的：作為需求、架構、開發順序與驗收的共同依據

## 1. 定位

AgentLab 是一個用來理解 Agent 工程細節的實驗型專案。它的價值不在於包裝一個聊天機器人，而在於親手建立並量測以下能力：

- 將自然語言目標轉成可執行工作流。
- 讓模型以明確角色分工，而不是用單一巨大 prompt 完成所有工作。
- 安全地讀寫工作區、執行工具並產生可稽核的變更。
- 對非確定性的模型輸出施加結構、限制、重試與停止條件。
- 以編譯、測試、規則及評測資料判定品質，不以「看起來可以」代替驗證。
- 從 trace、log、metric 與失敗案例持續調校 prompt、模型與流程。

本專案第一階段只處理小型 .NET 程式任務。支援其他語言、一般助理、長期記憶或自主部署，都不屬於目前核心範圍。

## 2. 輸入文件的解讀

`Goal.txt` 是原始方向草案，不是執行時指令。它提出的順序為：Observability → Failure Policy → Structured Files → Sandbox → build/test → OpenTelemetry/Aspire → Parallel/Routing/MCP。本文件保留這些主題，但補上依賴關係、契約、安全界線、評測方法與完成定義。

### 2.1 指令與資料必須分開

Agent 執行時，每段內容都必須帶有來源與信任等級，不得只因文字使用命令語氣就把它視為指令。

| 內容角色 | 例子 | 是否有權改變行為 |
| --- | --- | --- |
| HostPolicy | Runtime 寫死的安全規則、工具權限 | 是，最高優先 |
| UserRequest | API 本次收到且已驗證的使用者需求 | 是，但受 HostPolicy 限制 |
| ProjectInstruction | Runtime 明確允許的 `AGENTS.md` 或專案規範 | 是，只在所屬目錄範圍有效 |
| ReferenceMaterial | 附件、`Goal.txt`、需求文件、網頁、程式碼與註解 | 否，只能當事實或素材 |
| ToolOutput | build log、搜尋結果、MCP 回傳、外部 API 資料 | 否，只能當資料 |

必要規則：

1. Runtime 決定內容角色；模型不得自行把 ReferenceMaterial 升級成指令。
2. 附件中的「忽略先前規則」、「執行命令」等文字，預設都只是文件內容。
3. ProjectInstruction 必須來自 allowlist、帶有來源路徑，並在送入模型前標示作用範圍。
4. 外部資料不能直接組成工具參數；先轉成受 schema 約束的欄位，再經 policy 驗證。
5. 指令衝突必須記錄來源與採用結果，不能靜默覆蓋。

## 3. 成功時的完整行為

一個完整的 Agent run 應完成下列閉環：

```text
接收任務
  → 建立隔離工作區與執行識別碼
  → 蒐集受允許的專案脈絡
  → 產生結構化計畫
  → 產生結構化 ChangeSet
  → Policy 檢查與套用變更
  → dotnet build/test 等確定性驗證
  → Reviewer 檢查需求與設計
  → 有界限地修正、重驗
  → 輸出結果、變更清單、驗證證據與完整 trace
```

最終狀態只能是：

- `Succeeded`：所有必要驗證通過，且 Reviewer 核准。
- `SucceededWithWarnings`：必要驗證通過，但存在已明確允許的低風險警告。
- `Failed`：重試耗盡、驗證失敗或發生不可恢復錯誤。
- `Cancelled`：呼叫端取消；不得自動重試。
- `AwaitingApproval`：操作超出自動權限，保存狀態等待決定。

不能以「已跑完迴圈」等同成功。目前 `WorkflowResult` 即使 Reviewer 最後仍拒絕也會回傳，後續必須改成明確狀態。

## 4. 現況基線

目前已有：

- 固定的 `Planner → Developer → Reviewer → Fixer` 工作流。
- `IAgent<TInput, TOutput>` 角色介面。
- 透過 `Microsoft.Extensions.AI.IChatClient` 呼叫 Ollama，核心概念可維持 provider-neutral。
- Planner 與 Reviewer 的 JSON 反序列化。
- Developer/Fixer 的文字結果。
- 最多兩次 Fix 的有界迴圈。
- 基本 latency、token、finish reason log，以及 output truncation 偵測。
- 初步的 failure enum、execution context 與 execution result 型別。

目前主要缺口：

- `AgentExecutionContext`、`AgentExecutionResult` 尚未串入完整呼叫鏈。
- 只有 `length` 被正規化；timeout、空回應、invalid JSON、provider error 尚未統一分類。
- Planner/Reviewer 的 JSON 錯誤在 Agent 內變成一般例外，Failure Policy 無法判斷是否可重試。
- Implementation 是整塊 Markdown，不能安全、確定地套用檔案變更。
- 沒有 workspace sandbox、path policy、工具契約、原子寫入或變更稽核。
- Reviewer 只看模型文字，沒有真實 `dotnet build/test` 證據。
- prompt、模型、context size 與 token limit 寫死在類別中，無法做版本比較。
- workflow 沒有持久狀態、idempotency、預算、總逾時與恢復點。
- 沒有 eval dataset，prompt 調整無法量化比較。
- 沒有 OpenTelemetry trace/metric、routing、真正的平行工作或 MCP 邊界。

## 5. 架構原則

1. **確定性外殼，非確定性核心**：狀態機、權限、檔案操作、重試、驗證與停止條件由程式控制；模型只負責需要推理的步驟。
2. **先單一路徑，再增加自治**：先讓固定 workflow 可觀測、可靠、可評測，再導入 routing 與 parallel。
3. **資料契約優先**：Agent 之間傳 typed result，不傳自由格式長文。
4. **能力最小化**：每個角色只得到完成任務所需的 context 與工具。
5. **模型不判定自身權限**：所有 tool call 由 Runtime policy 核准或拒絕。
6. **失敗是領域資料**：錯誤類型、階段、是否可重試、證據與最後狀態必須保留。
7. **每次變更皆可比較**：prompt、模型、參數、工具 schema 與 policy 都必須有版本或 hash。
8. **成功必須有證據**：完成訊息必須連結到實際變更與 validator 結果。
9. **核心不綁 provider**：Ollama 是目前 adapter；Agent 與 orchestration 不應依賴 Ollama-specific option。

## 6. 目標元件

### 6.1 Orchestrator

負責狀態轉移、步驟順序、預算、重試、checkpoint 與取消，不負責撰寫 prompt 或解析 provider-specific response。

建議狀態：

```text
Accepted → Planning → Implementing → Applying
         → Validating → Reviewing → Fixing ─┐
                               ↑             │
                               └─────────────┘
         → Succeeded | Failed | Cancelled | AwaitingApproval
```

每個狀態必須列出：允許的下一狀態、進入條件、離開條件、最大嘗試次數與可保存的 checkpoint。

### 6.2 Agent Runtime

負責一次模型呼叫的共同處理：

- 建立 child span 與 correlation id。
- 套用模型設定、timeout 與 cancellation。
- 收集 usage、latency、finish reason 與 provider request id。
- 執行 structured-output validation。
- 將 provider 例外轉成統一的 `FailureInfo`。
- 交由 retry policy 決定重試，不讓各 Agent 自行複製 try/catch。

### 6.3 Agent definitions

保留目前四個角色，但縮小責任：

- Planner：把目標、限制、驗收條件轉成 `Plan`；不寫程式、不呼叫寫入工具。
- Developer：依 Plan 產生 `ChangeSet`；不能自行套用或宣告 build 通過。
- Reviewer：根據 goal、diff、validator 證據評估需求與設計；不修改檔案。
- Fixer：只處理明確 issue 與 validator error，產生增量 `ChangeSet`。

未來可新增 ContextCollector、Router 或 Specialist，但不得在沒有 eval 證據前拆分更多角色。

### 6.4 Tool Runtime

所有工具具有 typed schema、權限等級、timeout、輸出大小上限與 audit event。第一批工具只需要：

- `read_file`
- `list_files`
- `search_text`
- `write_file` 或 `apply_patch`
- `dotnet_build`
- `dotnet_test`

不得一開始就提供任意 shell。若之後加入，命令、參數、工作目錄、環境變數與網路權限都必須受 allowlist 控制。

### 6.5 Policy Engine

Policy Engine 在模型與工具之間執行，至少判斷：

- 路徑是否仍位於本次 sandbox。
- 操作是 read、create、update、delete、execute 或 external side effect。
- 是否超過檔案、命令、token、時間或重試預算。
- 是否需要人工核准。
- 是否接觸秘密、credential、`.git` 或受保護檔案。
- 工具參數是否來自未信任內容，且已通過 schema 驗證。

### 6.6 Validators / Evaluators

Validator 回答「結果是否客觀有效」；Reviewer 回答「結果是否符合意圖與設計」。兩者不能互相取代。

第一批確定性 validator：

- JSON/schema validation
- 路徑與 ChangeSet validation
- `dotnet build`
- `dotnet test`
- 變更範圍與禁止檔案檢查
- 無變更與空輸出檢查

之後才加入 analyzer、formatter、安全掃描及 LLM grader。

### 6.7 Artifact Store

每次 run 保存：request、採用的 project instructions、plan、每版 ChangeSet、diff、build/test 摘要、review、failure、prompt/config hash 與 trace id。原始 prompt/output 是否保存應由資料政策控制，預設不可寫入 secret。

## 7. 核心資料契約

型別名稱可以調整，但資訊不可省略。

```csharp
public sealed record AgentRunRequest(
    string Goal,
    string WorkspaceRoot,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<AcceptanceCriterion> AcceptanceCriteria,
    string? IdempotencyKey);

public sealed record RunContext(
    string RunId,
    string WorkflowId,
    string SandboxRoot,
    RunBudget Budget,
    InstructionSet Instructions,
    int Attempt);

public sealed record AgentStepResult<T>(
    StepStatus Status,
    T? Value,
    UsageInfo Usage,
    FailureInfo? Failure,
    IReadOnlyList<ArtifactRef> Artifacts);

public sealed record ChangeSet(
    string Summary,
    IReadOnlyList<FileChange> Files,
    IReadOnlyList<string> ExpectedValidations);

public sealed record FileChange(
    string RelativePath,
    FileChangeKind Kind,
    string? Content,
    string? ExpectedBeforeHash);

public sealed record ValidationReport(
    bool Passed,
    IReadOnlyList<ValidationCheck> Checks,
    IReadOnlyList<ArtifactRef> Evidence);
```

要求：

- 所有 enum 值封閉且未知值會被拒絕。
- `RelativePath` 禁止 absolute path、`..`、symlink/junction escape。
- update/delete 使用 before hash 防止覆蓋並行變更。
- JSON schema 與 C# model 必須由同一來源產生或以 contract test 保持一致。
- 大型內容存 artifact，只在 Agent 間傳 reference 與必要摘要。

## 8. Observability 規格

這是第一個實作里程碑，因為後續調校必須有基線。

### 8.1 Trace 階層

```text
WorkflowRun
  ├─ AgentStep(planner)
  │   └─ ModelCall
  ├─ AgentStep(developer)
  │   └─ ModelCall
  ├─ ToolCall(apply_changes)
  ├─ Validation(dotnet_build)
  ├─ AgentStep(reviewer)
  └─ AgentStep(fixer) × N
```

### 8.2 每次模型呼叫必填欄位

- `run.id`, `workflow.id`, `step.id`, `agent.name`, `attempt`
- provider、model、prompt version/hash、tool schema version
- start/end、duration、status、finish reason
- input/output/total tokens；若 provider 不提供，值為 unavailable，不假裝為 0
- timeout、retry count、context size、max output tokens
- failure type、provider code、是否可重試
- structured-output validation 結果

### 8.3 Metrics

- `agent_runs_total{agent,status}`
- `agent_run_duration_ms{agent}`
- `agent_tokens_total{agent,direction}`
- `agent_failures_total{agent,type}`
- `agent_retries_total{agent,type}`
- `workflow_runs_total{status}`
- `workflow_fix_cycles`
- `validation_runs_total{validator,status}`
- `change_files_total{kind}`

避免把 goal、prompt、完整檔案路徑等高基數或敏感值放進 metric label。

### 8.4 Log

使用 structured logging，不用 `Console.WriteLine`。log 只記索引資料與摘要；prompt、模型原文、檔案內容放入受控 artifact，並可設定完全不保存。

## 9. Failure Policy

所有 failure 至少包含：`Type`、`Stage`、`Message`、`Retryable`、`Attempt`、`EvidenceRef`、`ProviderCode` 與 `OccurredAt`。

| Failure | 預設處理 |
| --- | --- |
| Cancelled | 立即停止，不重試，狀態為 Cancelled |
| Timeout | 僅對該 step 重試，指數退避加 jitter，最多 2 次 |
| RateLimited / transient 5xx | 遵守 retry-after，受總時間與次數預算限制 |
| Authentication / configuration | fail fast，不重試 |
| OutputTruncated | 不用相同設定盲目重跑；先增加合理上限、分段或續寫一次 |
| EmptyResponse | 重試 1 次；再次發生即失敗 |
| InvalidStructuredOutput | 用 validation errors 做一次 repair；仍失敗則停止該 step |
| PolicyViolation | 拒絕操作；可安全縮小範圍時重規劃，否則等待核准或失敗 |
| ToolTransientError | 依工具的 idempotency 規則重試 |
| Build/TestFailed | 不算 provider retry；作為 Fixer 輸入，受 fix-cycle 上限控制 |
| BudgetExceeded | 保存 checkpoint 並失敗；不得暗中增加預算 |

全域限制：

- 每 step 最大模型嘗試次數。
- workflow 最大 Fix 次數。
- workflow wall-clock timeout。
- input/output token budget。
- 最大檔案數、總變更 bytes、工具呼叫數與命令輸出 bytes。
- 重試必須記錄原因；同一輸入與設定不得無限重播。

## 10. Structured Files 與變更套用

`Implementation(string Content)` 應被 `ChangeSet` 取代。Agent 不直接輸出「以下是程式碼」的 Markdown，而輸出機器可驗證的檔案操作。

套用流程：

1. 解析並驗證 schema。
2. canonicalize relative path。
3. 檢查 path policy、檔案大小、before hash 與操作權限。
4. 在 staging area 產生候選檔案。
5. 產生 diff 與 artifact。
6. 原子地套用到 sandbox。
7. 執行 validators。
8. 只有通過核准策略時，才把變更提交到真正工作區。

第一版允許 create/update；delete、rename、binary file 與大檔案延後，或一律需要人工核准。

## 11. Sandbox 與工具安全

每個 run 使用獨立目錄，來源工作區以受控方式複製或建立 worktree。Sandbox 必須具備：

- 明確且已解析的 root；所有 I/O 再驗證實際 resolved path。
- 禁止 `..`、absolute path、symlink/junction escape。
- `.git`、credential、系統目錄與設定的 protected paths。
- 原子寫入、before/after hash、diff 與 audit record。
- process timeout、輸出上限與可終止的 process tree。
- 預設關閉網路；若開啟，使用 host/domain allowlist。
- 最小環境變數；不得把 secret 放入 prompt 或 log。
- command allowlist。第一版只開放 `dotnet restore/build/test` 的受限 wrapper。
- 套用到使用者工作區前提供完整 change manifest；破壞性操作需要 approval。

模型只能提出 tool intent。Runtime 決定是否執行，並把結果以 untrusted ToolOutput 回傳。

## 12. Build/Test 與確定性 evaluator

建議迴圈：

```text
Developer ChangeSet
  → schema/policy/path checks
  → apply to sandbox
  → dotnet build
  → dotnet test
  → Reviewer
  → Fixer(ChangeSet + validation errors + review issues)
  → 重新執行全部必要檢查
```

原則：

- build 失敗時先提供 compiler errors 給 Fixer，避免浪費 Reviewer token。
- test 只以 exit code 不足以判斷，需保存 command、工作目錄、duration、exit code 與截斷後摘要。
- Reviewer 的 `Approved=true` 不能蓋過 build/test failure。
- validation 必須從乾淨或可重現的 sandbox 執行。
- 同一 ChangeSet 的 evaluator 結果以 content hash 快取；環境或依賴變更時失效。

## 13. Prompt、模型與調校方法

### 13.1 Prompt 管理

- prompt 移出 Agent 類別，放在有版本的資源中，例如 `Prompts/Planner/v1.md`。
- 每個 prompt 明確描述角色、輸入欄位、輸出 schema、拒絕條件與少量邊界例子。
- 將固定前綴與每次任務資料分開，方便快取與比較。
- prompt 變更必須附原因、預期指標與 eval 結果。
- 不同 Agent 不共享一段不斷增長的 system prompt。

### 13.2 Model profile

每個角色使用 `ModelProfile`，包含 provider、model、reasoning/think、context、output limit、timeout、成本權重與 fallback。Ollama-specific 設定留在 adapter，不進入核心 domain。

先用同一模型建立基線，再依資料判斷是否讓 Planner/Reviewer 使用較小或不同模型。沒有 eval 證據時不做 model routing。

### 13.3 調校循環

1. 從真實 failure 建立 eval case。
2. 固定 dataset 與環境，保存 baseline。
3. 一次只改一個主要變因：prompt、model、參數、tool schema 或 workflow。
4. 比較 task success、schema validity、build/test、review precision、latency 與 tokens。
5. 人工抽查 automated grader 的判定一致性。
6. 達到 gate 才升版；否則回復或記錄為失敗實驗。

Fine-tuning 不是起點。只有在 prompt、工具與 context 設計已穩定，且累積足夠、合法、品質一致的標註資料後才評估。

## 14. Eval 策略

建立版本化的 `evals/cases`。每個 case 包含 fixture repo、使用者 goal、限制、允許變更、禁止變更、必要驗證與預期失敗類型。

最小資料集至少覆蓋：

- 正常小型 API 功能。
- goal 含缺漏或歧義。
- 不需要變更的任務。
- Planner/Reviewer 回傳 malformed JSON。
- output truncation、timeout、provider transient error 與 cancellation。
- 初次 build 失敗但可修正。
- 無法修正且必須在預算內停止。
- 附件或 source code 含 prompt injection。
- path traversal、protected file、delete 與任意 shell 嘗試。
- 多語言、長 context、重複要求與互相衝突的素材。

評測層次：

1. Unit：schema、failure mapping、retry、path policy、state transition。
2. Contract：用 fake `IChatClient` 重播 length、empty、invalid JSON、429/5xx 等情境。
3. Integration：在暫存 fixture repository 套 ChangeSet 並 build/test。
4. Workflow eval：固定資料集比較 prompt/model 版本。
5. Adversarial：prompt injection、越權工具、資源耗盡與資料外洩測試。
6. Live provider：Ollama 或其他 provider 的 opt-in smoke test，不作為每次單元測試必要條件。

品質 gate：

- 安全與 path policy case 必須 100% 阻擋。
- `Succeeded` run 的必要 deterministic checks 必須 100% 通過。
- cancellation、不可重試錯誤與 budget exhaustion 必須在規定狀態停止。
- schema-valid rate、workflow success rate、latency 與 token 先量 baseline，再以 ADR 設門檻。
- 新版不可造成未說明的成功率退步；任何接受的退步必須換得明確成本或延遲收益。

官方 OpenAI 文件也建議以 task-specific eval 及早、持續評測，保留 log 來擴充案例，並以人工判斷校準自動評分。這些方法在本專案使用 Ollama 時仍然適用。

## 15. OpenTelemetry 與 Aspire

先完成內部 telemetry event/schema，再接 OpenTelemetry，避免把 provider 或 UI 概念滲入 domain。

導入順序：

1. `ActivitySource`：Workflow、Agent、Model、Tool、Validator spans。
2. `Meter`：第 8 節 metrics。
3. OTLP exporter 與 console exporter。
4. Aspire AppHost：本機啟動 API、Ollama 與 telemetry backend，查看 trace/log/metric。
5. dashboard 建立 workflow success、P50/P95 latency、tokens、retry、failure type 與 fix cycles。

OpenTelemetry 是標準介面；Aspire 是本機編排與觀察體驗，兩者不要和核心 runtime 綁死。

## 16. Routing、Parallel 與 MCP

這些能力排在可靠的單一路徑之後。

### 16.1 Routing

Router 只輸出封閉 enum，例如 `NoChange`、`SimpleEdit`、`Feature`、`Investigation`、`Unsupported`。路由結果必須可 trace、可 eval，且未知值 fail closed。

### 16.2 Parallel

只平行化互相獨立、沒有共享可變狀態的工作，例如：

- 讀取不同模組並產生摘要。
- 獨立的 test proposal 與 implementation proposal。
- 多個 Reviewer 依不同 rubric 評分。

同一批 agents 不可同時寫同一 sandbox。使用個別 branch/staging area，最後由單一 Merger 依 base hash 合併。小任務、固定相依步驟或同檔案修改維持循序執行。

### 16.3 MCP

MCP adapter 只暴露 allowlisted server/tool；schema、auth、approval、timeout、資料分類與 audit 必須與本地工具相同。MCP 回傳永遠是 untrusted ToolOutput，不能成為新 system instruction。

## 17. 建議專案結構

```text
AgentLab.Core/
  Contracts/
  Models/
  Orchestration/
  Runtime/
  Policies/
  Artifacts/
  Evaluation/

AgentLab.Infrastructure/
  Models/Ollama/
  Telemetry/
  Sandbox/
  Tools/
  Storage/

AgentLab.Api/
AgentLab.Tests.Unit/
AgentLab.Tests.Integration/
AgentLab.Evals/

Prompts/
evals/cases/
docs/decisions/
```

`AgentLab.Core` 不引用 Ollama、Aspire、OpenTelemetry exporter 或 process implementation；這些由 Infrastructure 實作介面。

## 18. 實作里程碑與完成定義

### M0 — Baseline 與契約

交付：

- 固定目前 4-Agent happy path 的 integration fixture。
- `RunContext`、`AgentStepResult<T>`、`FailureInfo`、`WorkflowStatus`。
- prompt/model config 可識別版本。
- `WorkflowResult` 能明確表示未核准、失敗與取消。

完成定義：現有功能不退步；fake chat tests 可重現成功、拒絕與最後未核准。

### M1 — Observability

交付：

- workflow/agent/model correlation。
- 統一 usage 與 latency result。
- structured log；移除 workflow 的 `Console.WriteLine`。
- metrics 介面與 in-memory 測試 collector。

完成定義：每個 model call 都可從 run id 追到 agent、attempt、tokens、latency、finish reason 與 prompt/config version。

### M2 — Failure Policy

交付：

- 完整 failure mapping。
- per-step timeout、retry/backoff、總預算與取消傳播。
- structured-output repair 一次。
- 狀態機及非法 transition tests。

完成定義：第 9 節每種 failure 都有 deterministic test；不發生無限 retry。

### M3 — Structured artifacts

交付：

- `ChangeSet`、`FileChange`、artifact manifest 與 JSON schema。
- Developer/Fixer 改傳 ChangeSet。
- path/before-hash/size validation。
- diff renderer。

完成定義：Implementation 不再是一大段 Markdown；malformed 或越權 change 在寫入前被拒絕。

### M4 — Sandbox 與檔案工具

交付：

- per-run isolated workspace。
- read/search/write/apply tool wrappers。
- atomic write、audit、protected paths 與 approval state。

完成定義：Agent 可在 fixture repo 完成 create/update；path traversal、symlink escape、delete 與 protected file cases 全部依 policy 阻擋。

### M5 — Deterministic validation

交付：

- process runner 與 `dotnet_build`/`dotnet_test`。
- validation report/evidence。
- compiler/test errors 進入 Fixer loop。
- clean/reproducible integration fixtures。

完成定義：只有 build/test 和 review 都符合規則才可標記 Succeeded；成功結果附可稽核證據。

### M6 — Eval harness

交付：

- versioned eval cases、runner、report 與 baseline comparison。
- prompt/model/config hash。
- regression gate 與人工抽查流程。

完成定義：任何 prompt、模型或 workflow 變更都能以同一資料集比較品質、延遲與 tokens。

### M7 — OpenTelemetry / Aspire

交付：

- traces、metrics、OTLP export、Aspire dashboard。
- 不記錄敏感內容的 telemetry policy。

完成定義：能從 dashboard 找到一次 run 的所有 Agent、tool、validation、retry 與 failure；metric labels 無高基數內容。

### M8 — Routing / Parallel / MCP

交付：

- 先做 routing eval，再啟用 router。
- 至少一個真正可安全平行的 read-only use case。
- shared-state merge policy。
- 一個 allowlisted MCP server 的 adapter、approval 與 audit。

完成定義：與循序 baseline 比較後，平行或 routing 帶來可量測收益，且沒有品質或安全 gate 退步。

### M9 — Production hardening（選做）

交付：

- run persistence、checkpoint/resume、idempotency。
- authentication/authorization、rate limiting、retention、secret management。
- health checks、deployment/rollback、schema migration 與 incident runbook。

完成定義：process restart 後可安全恢復或明確終止；重送同一 idempotency key 不會重複外部副作用。

## 19. 每個里程碑的工作規則

每次實作都遵循：

1. 先選一個未完成 milestone，建立一個小型 vertical slice。
2. 先寫或補對應的 acceptance/eval case。
3. 修改契約與實作；不要同時進行無關重構。
4. 執行 unit、integration 與必要 live smoke test。
5. 比較 baseline：成功率、failure、latency、tokens 與 fix cycles。
6. 更新本文件的狀態與一份 ADR；記錄決策、替代方案、結果與已知限制。
7. 只有完成定義全部滿足，才進入下一 milestone。

PR/變更說明至少包含：目標、scope、資料契約變化、風險、測試證據、telemetry 影響與 rollback 方法。

## 20. 暫不處理

在 M0–M6 完成前，不優先做：

- 長期記憶、向量資料庫或人格系統。
- 讓 Agent 自己無限制建立 Agent。
- 任意 shell、任意網路、直接 production deployment。
- 依直覺增加更多 Reviewer 或多模型投票。
- GUI、Marketplace 或通用 plugin 生態。
- Fine-tuning。

這些項目不是永久排除，而是必須等到核心流程能被量測、限制與驗證後，再用具體 use case 和 eval 證明價值。

## 21. 近期三個實作切片

下一步依序執行：

1. **Execution result 垂直切片**：讓 `AgentExecutor` 回傳帶 usage/failure 的 typed result，並把 run/workflow/attempt context 串過四個 Agent。
2. **Failure normalization 垂直切片**：把 empty、invalid JSON、timeout、cancel、length 與 provider error 全部映射成 `FailureInfo`，用 fake client 寫 deterministic tests。
3. **ChangeSet 垂直切片**：先讓 Developer 對單一 fixture 產生 create/update JSON，不寫真實工作區；通過 schema/path/hash tests 後才接 sandbox write。

完成這三片後，才開始 process runner 與 `dotnet build/test`。

## 22. 參考依據

以下官方 OpenAI 文件只作為 Agent 工程方法參考；目前實作仍以 `Microsoft.Extensions.AI` + Ollama 為主，不要求改用 OpenAI API：

- [Evaluation best practices](https://developers.openai.com/api/docs/guides/evaluation-best-practices)：eval-driven development、task-specific cases、持續評測與人工校準。
- [Safety in building agents](https://developers.openai.com/api/docs/guides/agent-builder-safety)：structured outputs、tool approvals、guardrails、trace graders，以及隔離未信任資料。
- [Multi-agent](https://developers.openai.com/api/docs/guides/responses-multi-agent)：適合平行化的工作、shared mutable state 的限制與 token 成本。
- [Create an agent API reference](https://developers.openai.com/api/reference/typescript/resources/beta/subresources/agents/methods/create)：可重用 Agent 的 instructions、tools、reasoning 與 multi-agent 等配置面向。

## 23. 文件維護

- 本文件是 roadmap 與架構契約，不取代各功能的 ADR、API schema 或測試。
- 每完成一個 milestone，更新狀態、實際數據與偏離原因。
- 若程式與本文件衝突，在同一變更中修正其中一方，不留下無說明的落差。
- 安全邊界、成功定義與資料契約的改動必須有 ADR。
- 時效性外部資訊需重新查閱官方文件，不以本文件的日期作為永久事實。
