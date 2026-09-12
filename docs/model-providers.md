# 模型供應與切換設定

AgentLab 透過具名 profile 選擇模型。Core 只依賴 `IAgentModelResolver` 與 `IChatClient`，不再直接依賴 Ollama 或雲端 SDK。

## 內建 provider

| Provider | 用途 | 必要設定 |
| --- | --- | --- |
| `Ollama` | Ollama 原生地端服務 | `Model`、`Endpoint` |
| `OpenAICompatible` | OpenAI API 或相容的雲端／自架 endpoint | `Model`、API key 環境變數；自訂服務另加 `Endpoint` |

其他雲端 API 若不相容 OpenAI protocol，可以實作並註冊新的 `IAgentModelProviderFactory`，不需修改 Core 或任何 Agent。

## 使用 Ollama

`AgentLab.Api/appsettings.json` 預設使用：

```json
{
  "AgentModels": {
    "DefaultProfile": "local",
    "Agents": {},
    "Profiles": {
      "local": {
        "Provider": "Ollama",
        "Model": "qwen3.5:9b",
        "Endpoint": "http://localhost:11434",
        "TimeoutSeconds": 300,
        "ContextWindow": 8192,
        "ThinkByAgent": {
          "planner": false,
          "developer": true,
          "reviewer": false,
          "fixer": true
        }
      }
    }
  }
}
```

Ollama 專屬的 `Think` 與 `ContextWindow` 由 Infrastructure adapter 套用，Agent classes 不會引用 Ollama 型別。

## 切換成雲端 OpenAI-compatible API

不要把 API key 寫入 JSON。先設定環境變數，例如 `AGENTLAB_CLOUD_API_KEY`，再加入 cloud profile：

```json
{
  "AgentModels": {
    "DefaultProfile": "cloud",
    "Agents": {},
    "Profiles": {
      "cloud": {
        "Provider": "OpenAICompatible",
        "Model": "YOUR_MODEL_ID",
        "ApiKeyEnvironmentVariable": "AGENTLAB_CLOUD_API_KEY",
        "TimeoutSeconds": 120
      }
    }
  }
}
```

直接使用 OpenAI API 時省略 `Endpoint`。若是相容 protocol 的代理或自架服務，加入它的 API base URL：

```json
{
  "Provider": "OpenAICompatible",
  "Model": "YOUR_MODEL_ID",
  "Endpoint": "https://your-provider.example/v1",
  "ApiKeyEnvironmentVariable": "AGENTLAB_CLOUD_API_KEY",
  "TimeoutSeconds": 120
}
```

實際 model id、endpoint 與 credential 規則由該供應商決定。

## 混合使用雲端與地端模型

`Agents` 可以覆蓋 `DefaultProfile`。例如讓 Developer/Fixer 在地端執行，只讓 Planner/Reviewer 使用雲端模型：

```json
{
  "AgentModels": {
    "DefaultProfile": "local",
    "Agents": {
      "planner": "cloud",
      "reviewer": "cloud"
    },
    "Profiles": {
      "local": {
        "Provider": "Ollama",
        "Model": "qwen3.5:9b",
        "Endpoint": "http://localhost:11434",
        "ContextWindow": 8192
      },
      "cloud": {
        "Provider": "OpenAICompatible",
        "Model": "YOUR_MODEL_ID",
        "ApiKeyEnvironmentVariable": "AGENTLAB_CLOUD_API_KEY"
      }
    }
  }
}
```

未列在 `Agents` 的角色使用 `DefaultProfile`。Profile 以 singleton lazy 建立，同一 profile 可安全重複使用。

目前 configuration 會在應用程式啟動時建立快照；修改 profile 後需重新啟動 API。Runtime hot swap 涉及進行中 request、client disposal 與 eval 可重現性，留待後續以獨立里程碑處理。

## 設定驗證與診斷

應用程式啟動時會驗證：

- `DefaultProfile` 必須存在。
- 每個 Agent override 必須指向已知 profile。
- 每個 profile 必須有 provider 與 model。
- endpoint 必須是 absolute HTTP/HTTPS URI。
- Ollama 必須設定 endpoint。
- timeout 與 context window 必須在允許範圍。

雲端 profile 第一次被選取時才讀取指定的 API key 環境變數；缺少 key 會回傳包含 profile 與變數名稱的明確錯誤，不會無聲改用地端模型。

可呼叫 `GET /ai/model/{agentName}` 確認某個 Agent 解析後使用的 profile、provider 與 model。此 endpoint 不回傳 API key。

## 新增不同 protocol 的 provider

1. 在 Infrastructure 新增 `IAgentModelProviderFactory` 實作。
2. 使用唯一的 `ProviderName`。
3. 將供應商 SDK client 包成 `Microsoft.Extensions.AI.IChatClient`。
4. 將 provider-specific options 留在 adapter 的 `ConfigureOptions`。
5. 在 DI 註冊 factory。
6. 加入 options validation、resolver test 與 opt-in integration test。

不得在 Planner、Developer、Reviewer、Fixer 或 `AgentExecutor` 中加入 provider 名稱判斷。
