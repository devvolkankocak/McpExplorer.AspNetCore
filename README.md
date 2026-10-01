# McpExplorer.AspNetCore

Swagger/Scalar-style interactive UI for Model Context Protocol (MCP) servers in ASP.NET Core.
Add one line (`app.MapMcpExplorer()`) to browse all MCP tools, fill schema-generated forms, send requests with
custom headers (Authorization, tokens) and inspect responses.

`mcp` · `model-context-protocol` · `swagger` · `scalar` · `ui` · `aspnetcore`

---

Swagger UI / Scalar benzeri, **MCP (Model Context Protocol) sunucularını test etmek için** ASP.NET Core arayüzü.
Projeye ekleyince tüm MCP tool'larını (ad, açıklama, input şeması, annotation'lar) listeler, şemadan otomatik form
üretir ve tool'u çağırıp sonucu gösterir.

## Kullanım

```csharp
builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly();

var app = builder.Build();
app.MapMcp("/mcp");

if (app.Environment.IsDevelopment())
    app.MapMcpExplorer("/mcp-explorer");          // => http://localhost:5000/mcp-explorer/

app.Run();
```

### Seçenekler

```csharp
app.MapMcpExplorer("/mcp-explorer", o =>
{
    o.McpEndpoint = "/mcp";              // göreli yol veya https://baska-sunucu/mcp
    o.Title = "Benim MCP Sunucum";
    o.AllowCustomEndpoint = true;        // UI'dan başka bir MCP URL'si girilebilsin (sadece güvenilir ortamda)
    o.ForwardedHeaders.Add("X-Api-Key"); // tarayıcı isteğinden MCP'ye aktarılacak header'lar (varsayılan: Authorization)
    o.AdditionalHeaders["X-Env"] = "dev";
    o.RequestTimeout = TimeSpan.FromSeconds(30);
});
```

### Projeye özel header'lar

Her MCP sunucusunun beklediği header'lar farklı olabilir (Authorization, refresh token, tenant, trace id...).
Bunları kodda tanımlarsın; UI'daki **Headers** panelinde açıklamasıyla listelenir:

```csharp
app.MapMcpExplorer("/mcp-explorer", o =>
{
    o.AddHeader("Authorization", h =>
    {
        h.Required = true;                       // doldurulmadan bağlanılmaz / tool çağrılmaz
        h.Secret = true;                         // değer maskelenir (Show/Hide)
        h.Placeholder = "Bearer <access-token>";
        h.Description = "API erişim token'ı";
    });
    o.AddHeader("X-Refresh-Token", h => h.Secret = true);
    o.AddHeader("X-Tenant-Id", h => h.DefaultValue = "acme");

    o.AdditionalHeaders["X-Internal-Key"] = "...";   // sadece sunucuda eklenir, tarayıcıya hiç gitmez
});
```

- Tanımlı header'ların yanında, kullanıcı panelden istediği **ek header'ı** da elle ekleyebilir.
- Her header tek tıkla açılıp kapatılabilir; değerler tarayıcıda (localStorage) saklanır.
- Header'lar hem `tools/list` hem `tools/call` isteğine eklenir.
- Öncelik sırası (düşükten yükseğe): `ForwardedHeaders` (tarayıcı isteğinden) → UI'da girilen değerler → `AdditionalHeaders`.
- Zorunlu header eksikse UI uyarır ve sunucu tarafı da `400` ile reddeder.
- `DefaultValue` tarayıcıya gönderilir; gerçek gizli değerleri `AdditionalHeaders`'a koy.

`MapMcpExplorer` bir `IEndpointConventionBuilder` döner, yani `.RequireAuthorization()` vb. eklenebilir.

## Özellikler

- Tool listesi + arama (ad / açıklama)
- JSON Schema'dan form: string (date, date-time, email, uri formatları), number/integer (min/max), boolean, enum → select,
  iç içe object, primitive array (satır ekle/sil), karmaşık tipler için JSON editör; `default` değerleri doldurulur
- Form / JSON / Schema sekmeleri (outputSchema dahil), zorunlu alan ve tip doğrulaması
- Sonuç görünümü: text (JSON ise pretty-print), image, audio, resource, resource_link, `structuredContent`,
  `isError` rozeti, süre (ms), ham yanıt
- Annotation rozetleri: read-only, destructive, idempotent, open-world
- "Copy JSON-RPC" ile ham `tools/call` isteğini kopyalama
- Projeye özel header tanımları (zorunlu, gizli, varsayılan değer) + serbest header editörü
- Açık/koyu tema, mobil uyumlu, `#tool_adi` ile derin link

## Nasıl çalışır

UI, kütüphanenin içine gömülü tek bir HTML dosyasıdır (CDN bağımlılığı yok). Tarayıcı MCP'ye doğrudan bağlanmaz;
kütüphanenin küçük proxy API'sini çağırır, o da resmi `ModelContextProtocol` C# SDK'sının `McpClient`'ı ile
MCP sunucusuna (Streamable HTTP / SSE) bağlanır:

| Endpoint | Açıklama |
|---|---|
| `GET  {prefix}/` | UI |
| `GET  {prefix}/api/config` | Başlık, varsayılan endpoint |
| `POST {prefix}/api/connect` | `initialize` + `tools/list` (sayfalı) |
| `POST {prefix}/api/call` | `tools/call` |

Bu sayede CORS sorunu olmaz ve aynı UI uzak MCP sunucularını da test edebilir.

## Proje yapısı

```
src/McpExplorer.AspNetCore          NuGet kütüphanesi (MapMcpExplorer + gömülü UI)
samples/ExampleBackendApi     Gerçekçi backend örneği: REST controller + Scalar + MCP tool'ları + McpExplorer
                              (kütüphaneyi packages/ klasöründeki .nupkg'den kullanır)
samples/SampleMcpServer       Tüm form tiplerini gösteren demo sunucu (kütüphaneye ProjectReference)
packages/                     McpExplorer.AspNetCore.0.2.0.nupkg (yerel NuGet kaynağı)
nuget.config                  nuget.org + packages/ kaynakları
```

## Lokalde çalıştırma

Gereken: örnekleri bu repodan derlemek için [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). `SampleMcpServer` .NET 8 ve 9'da da çalışır: `dotnet run -f net8.0`.

```bash
cd McpExplorer/samples/ExampleBackendApi
dotnet run
```

Ardından tarayıcıda:

| Adres | Ne var |
|---|---|
| http://localhost:5100/mcp-explorer/ | MCP tool'larını test etme arayüzü (bu kütüphane) |
| http://localhost:5100/scalar | Aynı backend'in REST API dokümantasyonu |
| http://localhost:5100/api/products | REST endpoint |
| http://localhost:5100/mcp | MCP endpoint'i (Claude, VS Code vb. buraya bağlanır) |

Visual Studio / Rider ile: `McpExplorer.slnx`'i aç, `ExampleBackendApi`'yi startup project yap, F5.

## Kendi projene ekleme

Desteklenen hedefler: **.NET 8, .NET 9, .NET 10** (paket üçünü de içerir, NuGet doğru olanı seçer).
ModelContextProtocol SDK: **1.0.0 ve üzeri** (1.x ve 2.x). Projendeki sürüm neyse o kullanılır, paket seni yükseltmeye zorlamaz.

nuget.org'dan:

```bash
dotnet add package McpExplorer.AspNetCore
```

Ya da yayınlanmamış bir sürümü yerel `.nupkg` ile:

1. `McpExplorer.AspNetCore.0.2.0.nupkg` dosyasını bir klasöre koy (ör. `C:\nuget-local`).
2. Kaynak olarak ekle ve paketi kur:
   ```bash
   dotnet nuget add source C:\nuget-local --name McpExplorer-local
   dotnet add package McpExplorer.AspNetCore --version 0.2.0
   ```
   (Ya da bu repodaki gibi proje köküne `nuget.config` koyup klasörü orada tanımla.)
3. MCP sunucun zaten varsa `Program.cs`'e tek satır:
   ```csharp
   app.MapMcp("/mcp");
   if (app.Environment.IsDevelopment())
       app.MapMcpExplorer("/mcp-explorer");
   ```

## Yayınlama

`vX.Y.Z` etiketi ya da `release/vX.Y.Z` dalı push edilince `.github/workflows/publish.yml` paketi o sürümle nuget.org'a gönderir ve
`.nupkg` dosyasını GitHub Release'e ekler. nuget.org Trusted Publishing kullanılır (API key gerekmez):
nuget.org'da `developervolkan` hesabında bu repo ve `publish.yml` için bir policy tanımlıdır.

NuGet paketini yeniden üretmek için: `dotnet pack src/McpExplorer.AspNetCore -c Release -o packages`

## Güvenlik notu

UI tool'ları gerçekten çalıştırır ve `AllowCustomEndpoint` açıkken sunucunuz üzerinden başka adreslere istek atar.
Production'da kapatın ya da `.RequireAuthorization()` ile koruyun.

## Yol haritası (öneri)

Resources ve Prompts sekmeleri, progress/log bildirimleri (SSE ile canlı), istek geçmişi, stdio sunucular için destek,
OAuth akışı.

## Lisans

[MIT](LICENSE)
