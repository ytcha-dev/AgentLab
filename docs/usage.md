# AgentLab 當前版本操作指南

> 適用範圍：模型 provider 設定與 M0 typed execution result 切片完成後的版本。

## 1. 前置條件

- .NET 10 SDK。
- 預設使用 Ollama：本機 Ollama 已啟動，且已有 `qwen3.5:9b`。
- 或依 `docs/model-providers.md` 設定 OpenAI-compatible endpoint；API key 只放在指定環境變數，不寫入 repository。

使用預設模型前可執行：

```powershell
ollama pull qwen3.5:9b
```

## 2. 建置與測試

在 repository 根目錄執行：

```powershell
dotnet restore .\AgentLab.slnx
dotnet build .\AgentLab.slnx --no-restore
dotnet test .\AgentLab.Infrastructure.Tests\AgentLab.Infrastructure.Tests.csproj --no-restore --no-build
```

成功時 build 應為 0 errors，tests 應全部通過。Deterministic tests 不需要啟動 Ollama。

## 3. 啟動 API

```powershell
dotnet run --project .\AgentLab.Api\AgentLab.Api.csproj --launch-profile https
```

預設位址是 `http://localhost:5004` 與 `https://localhost:7195`。若缺少 HTTPS 開發憑證，可先執行 `dotnet dev-certs https`，或只以 HTTP 啟動：

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run `
    --project .\AgentLab.Api\AgentLab.Api.csproj `
    --no-launch-profile `
    --urls http://localhost:5004
```

確認 API 與模型解析：

```powershell
Invoke-RestMethod http://localhost:5004/ping
Invoke-RestMethod http://localhost:5004/ai/model/planner
```

第二個命令應回傳 planner 實際解析到的 `profileName`、`provider` 與 `model`，不會回傳 API key。

## 4. 執行完整 Workflow

```powershell
$requestBody = @{
    goal = "Build a minimal ASP.NET Core Todo API."
} | ConvertTo-Json

$result = Invoke-RestMethod `
    -Method Post `
    -Uri http://localhost:5004/agent/run `
    -ContentType "application/json" `
    -Body $requestBody

$result | ConvertTo-Json -Depth 10
```

主要回應欄位：

- `run.runId`：同一次 Workflow 的 correlation id。
- `status`：`Succeeded`、`NotApproved`、`Failed` 或 `Cancelled`。
- `plan`、`implementation`、`review`：目前產出的 typed artifacts。
- `steps[]`：每次模型呼叫的 Agent、step、attempt、model、usage、duration、finish reason 與 failure。
- `failure`：terminal failure；成功及單純 `NotApproved` 時為 `null`。

`Reviewer` 回傳拒絕不代表 model call 失敗，因此對應 step 仍可為 `Succeeded`；若兩次 Fix 後第三次 Review 仍拒絕，最終 Workflow status 為 `NotApproved`。

## 5. 單段 Agent endpoint

也可以使用既有 endpoint 觀察部分流程：

- `POST /agent/plan`
- `POST /agent/develop`
- `POST /agent/review`
- `GET /ai/test`

它們現在會一併回傳 run／execution metadata。請以 `/agent/run` 作為完整 terminal status 的主要入口。

## 6. 當前限制與安全注意事項

- `implementation` 目前仍是模型產生的文字，不會寫入檔案，也不會自動執行其中的命令。
- 尚未建立 sandbox、ChangeSet、`dotnet build/test` evaluator；`Succeeded` 目前只代表既有 Agent workflow 完成且 Reviewer 核准，不代表產出的程式已實際編譯。
- Empty response、invalid JSON、timeout 與 transient provider error 尚未套用完整分類／retry policy。
- Model profile 設定變更後需要重新啟動 API。
- API 目前沒有 authentication，不應直接暴露到不受信任網路。
- 按 `Ctrl+C` 停止本機 API。

模型切換與 per-Agent profile 範例見 `docs/model-providers.md`。
