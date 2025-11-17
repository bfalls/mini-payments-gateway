# Mini Payments Gateway (C# / .NET 8)
Idempotent payments API with Outbox pattern, worker dispatch, and a PSP stub. Built to demo reliability patterns for high-volume payments.

## Projects

| Project         | Description                    |
|------------------|------|
| Gateway.Api | Minimal API (+ Swagger) |
| Gateway.Domain | Entities/DTOs |
| Gateway.Data | EF Core DbContext (+ Outbox/Idempotency) |
| Gateway.Worker | Background dispatcher |
| PspStub.Api | Fake Payment Service Provider for auth/testing |

## Quick start
dotnet build

## PrivateCircle demo UI
- Run `Gateway.Api` and browse to [http://localhost:5023](http://localhost:5023) to use the built-in PrivateCircle-branded page. It can derive idempotency keys for fiat and crypto payloads, submit payments, and show canonical payloads used by the middleware.

### Online demo: worker updates for fiat + crypto
Use the built-in UI at http://localhost:5023 to show the background worker updating payment records after the simulated PSP/chain responds.

1. Start **PspStub.Api**, **Gateway.Api**, and **Gateway.Worker** (see steps below). Keep the demo UI open in a browser.
2. In the **Card / Fiat** card, click **Derive key** to populate the derived key and canonical payload, then click **Submit payment**. The UI displays the initial `Pending` response and fills the **Payment ID** field.
3. Click **Fetch /payments/{paymentId}** to query the API from inside the demo. As the worker authorizes the payment, repeat the fetch to watch the status move to `Authorized` with an `authCode` from the PSP stub.
4. Repeat the flow in the **Crypto** card. After **Submit crypto**, use **Fetch /payments/{paymentId}** and **Fetch /payments/{paymentId}/crypto** to watch the worker mark the transaction with confirmations and authorize the payment using the simulated `txHash`.
5. Call either submit button with the same derived key to highlight that the stored response is replayed (idempotent behavior still works after the worker updates records).
 
## Let's test it

### Services and ports used

| Service          | URL | Notes                          |
|------------------|------|--------------------------------|
| Postgres        | port 5432<br>Powershell Test:<br> `Test-NetConnection -Port 5432 -ComputerName localhost` | Database  |
| Gateway.Api     | [http://localhost:5023/health](http://localhost:5023/health)<br>[http://localhost:5023/swagger](http://localhost:5023/swagger)  | Payments API  |
| PspStub.Api     | [http://localhost:5279](http://localhost:5279) | fake Payment Service Provider used for testing  |
| Gateway.Worker  | N/A  | Background processor. Reads pending outbox messages and calls the PSP to authorize/decline payments. |
| Gateway.Domain  | N/A  | Library: Domain model layer. Contains business entities (`Payment`, etc.) and domain logic shared across services.  |
| Gateway.Data   | N/A  | Library: Data access layer. Contains EF Core `GatewayDbContext`, entity mapping, and database migration logic.  |

### Testing
These are mostly PowerShell commands.

1. Create and Run Postgres in Docker:
  ```powershell
  docker run --name payments-pg `
  -e POSTGRES_PASSWORD=postgres `
  -e POSTGRES_DB=gateway `
  -p 5432:5432 `
  -d postgres:16
  ```
  - Verify with:
```powershell
docker ps
```
  - If payments-pg already exists then just start it with:
```powershell
docker start payments-pg
```

You should see payments-pg running on port 5432.

2. Create the database schema:
  ```powershell
  dotnet ef database update `
  --project .\src\Gateway.Data\Gateway.Data.csproj `
  --startup-project .\src\Gateway.Api\Gateway.Api.csproj
  ```
Verify the tables were created:
  ```powershell
  docker exec -it payments-pg psql -U postgres -d gateway -c "\dt"
  ```
You should see the three tables and a history table.

3. Open four terminals. Set the current directory to the solution root in each.
4. In terminal 1, start the PSP stub:
  ```powershell
  dotnet run --project .\src\PspStub.Api\PspStub.Api.csproj
  ```
  - Browse to [http://localhost:5279/](http://localhost:5279/) to see the PSP is working.

5. In terminal 2, start Gateway.Api (payments API) default port is 5023
```powershell
dotnet run --project .\src\Gateway.Api\Gateway.Api.csproj
```
Browse to [http://localhost:5023/health](http://localhost:5023/health)

Browse to [http://localhost:5023/swagger](http://localhost:5023/swagger) to see the API docs.

6. In terminal 3, start Gateway.Worker (background processor)

Gateway.Worker depends on:
- Postgres
- The PSP base URL configured in appsettings.json
  ```powershell
  dotnet run --project .\src\Gateway.Worker\Gateway.Worker.csproj
  ```

7. In PowerShell terminal 4, run the test with derived idempotency keys (no more hard-coded demo keys)
- First, ask the API to derive the canonical payload + key that the middleware uses:
  ```powershell
  $derive = Invoke-WebRequest `
    -Uri "http://localhost:5023/tools/derive-idempotency/charge" `
    -Method POST `
    -Headers @{ "x-api-key" = "local-dev" } `
    -ContentType "application/json" `
    -Body '{ "amount":4200,"currency":"USD","sourceToken":"tok_visa","merchantRef":"order-1001" }'

  $derivedKey = ($derive.Content | ConvertFrom-Json).derivedKey
  Write-Host "Derived key:" $derivedKey -ForegroundColor Cyan
  ```

- Create the request using that derived key (the server re-computes it and rejects mismatches):
  ```powershell
  $response = Invoke-WebRequest `
    -Uri "http://localhost:5023/payments/charge" `
    -Method POST `
    -Headers @{ "x-api-key" = "local-dev"; "Idempotency-Key" = $derivedKey } `
    -ContentType "application/json" `
    -Body '{ "amount":4200,"currency":"USD","sourceToken":"tok_visa","merchantRef":"order-1001" }'

  $data = $response.Content | ConvertFrom-Json
  $paymentId = $data.paymentId
  Write-Host ($data | ConvertTo-Json -Depth 5)
  ```
- Immediately check status (expect Pending) and then after a few seconds check again (should be Authorized with an authCode):
    ```powershell
    Invoke-WebRequest `
      -Uri "http://localhost:5023/payments/$paymentId" `
      -Headers @{ "x-api-key" = "local-dev" } |
    Select-Object StatusCode, Content
    ```

- To prove idempotency, repeat the charge with the **same body** and **same derived key**. You should receive the exact same paymentId and payload because the middleware replays the stored response for the canonical hash:
  ```powershell
  $resp = Invoke-WebRequest `
    -Uri "http://localhost:5023/payments/charge" `
    -Method POST `
    -Headers @{ "x-api-key" = "local-dev"; "Idempotency-Key" = $derivedKey } `
    -ContentType "application/json" `
    -Body '{ "amount":4200,"currency":"USD","sourceToken":"tok_visa","merchantRef":"order-1001" }'
   Write-Host "StatusCode: $($resp.StatusCode)"
   ($resp.Content | ConvertFrom-Json) | ConvertTo-Json -Depth 10  
  ```


## Blockchain / Crypto Demo Mode
A lightweight crypto simulation is wired into the same payment/outbox pipeline to show how blockchain-style flows could be modeled without adding real chain dependencies.

1. Submit a crypto charge (derive the key first):
   ```powershell
   $cryptoDerive = Invoke-WebRequest `
     -Uri "http://localhost:5023/tools/derive-idempotency/crypto-charge" `
     -Method POST `
     -Headers @{ "x-api-key" = "local-dev" } `
     -ContentType "application/json" `
     -Body '{
       "amount": 500000,
       "cryptoCurrency": "USDC",
       "network": "Ethereum-Testnet",
       "fromWallet": "0xCafeFood00000000000000000000000000000000",
       "merchantRef": "order-crypto-42"
     }'
   $cryptoKey = ($cryptoDerive.Content | ConvertFrom-Json).derivedKey
   Write-Host "Derived crypto key:" $cryptoKey -ForegroundColor Cyan

   $cryptoCharge = Invoke-WebRequest `
     -Uri "http://localhost:5023/payments/crypto-charge" `
     -Method POST `
     -Headers @{ "x-api-key" = "local-dev"; "Idempotency-Key" = $cryptoKey } `
     -ContentType "application/json" `
     -Body '{
       "amount": 500000,
       "cryptoCurrency": "USDC",
       "network": "Ethereum-Testnet",
       "fromWallet": "0xCafeFood00000000000000000000000000000000",
       "merchantRef": "order-crypto-42"
     }'
   Write-Host "Crypto charge response:" -ForegroundColor Cyan
   Write-Host ($cryptoCharge.Content | ConvertFrom-Json | ConvertTo-Json -Depth 5)
   ```
   The API stores a normal `Payment`, creates a `CryptoTransaction` row in `Pending`, and enqueues a `CryptoConfirm` outbox message. The response includes the generated `txHash` so you can track it.

2. Let the worker pick up the new outbox entry. Instead of calling the PSP stub, the worker simulates chain confirmations by marking the crypto transaction confirmed (3 confirmations) and authorizing the payment with the `txHash` as the auth code.

3. Poll the payment or its crypto transaction record:
   ```powershell
   $paymentId = ($cryptoCharge.Content | ConvertFrom-Json).paymentId
   Write-Host "Get Payment" -ForegroundColor Cyan
   $resp = Invoke-WebRequest `
   -Uri "http://localhost:5023/payments/$paymentId" `
   -Headers @{ "x-api-key" = "local-dev" }
   Write-Host "StatusCode: $($resp.StatusCode)"
   ($resp.Content | ConvertFrom-Json) | ConvertTo-Json -Depth 10
   Write-Host "Get Crypto Transaction" -ForegroundColor Cyan
   $resp = Invoke-WebRequest `
   -Uri "http://localhost:5023/payments/$paymentId/crypto" `
   -Headers @{ "x-api-key" = "local-dev" }
   Write-Host "StatusCode: $($resp.StatusCode)"
   ($resp.Content | ConvertFrom-Json) | ConvertTo-Json -Depth 10
   ```
   The crypto endpoint returns network, wallet, confirmations, and timestamps for the simulated transaction.

All crypto charge requests are idempotent just like card charges—the `IdempotencyMiddleware` now protects both `/payments/charge` and `/payments/crypto-charge`.


 
