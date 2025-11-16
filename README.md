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

7. In PowerShell terminal 4, run the test
- Create the request and query the payment status immediately:
  ```powershell
  $response = Invoke-WebRequest `
    -Uri "http://localhost:5023/payments/charge" `
    -Method POST `
    -Headers @{ "x-api-key" = "local-dev"; "Idempotency-Key" = "abc-123" } `
    -ContentType "application/json" `
    -Body '{ "amount":4200,"currency":"USD","sourceToken":"tok_visa","merchantRef":"order-1001" }'

  Write-Host "Response:" -ForegroundColor Cyan
  Write-Host ($response.Content | ConvertFrom-Json | ConvertTo-Json -Depth 5)

  $data = $response.Content | ConvertFrom-Json
  $paymentId = $data.paymentId

  Invoke-WebRequest `
  -Uri "http://localhost:5023/payments/$paymentId" `
  -Headers @{ "x-api-key" = "local-dev" } |
  Select-Object StatusCode, Content
  ```
  - See that the status is zero (Pending) and there is no authCode.

- Wait a few seconds for the worker to process the outbox and call the PSP. Query the same payment again.
    ```powershell
    Invoke-WebRequest `
    -Uri "http://localhost:5023/payments/$paymentId" `
    -Headers @{ "x-api-key" = "local-dev" } |
    Select-Object StatusCode, Content
    ```
    - You should see the status is now 1 (Authorized) and there is an authCode. This tests that the `Gateway.Worker` is operating.

- To test idempotency, re-run the first request with the same Idempotency-Key:
  ```powershell
  $response2 = Invoke-WebRequest `
    -Uri "http://localhost:5023/payments/charge" `
    -Method POST `
    -Headers @{ "x-api-key" = "local-dev"; "Idempotency-Key" = "abc-123" } `
    -ContentType "application/json" `
    -Body '{ "amount":4200,"currency":"USD","sourceToken":"tok_visa","merchantRef":"order-1001" }'
  Write-Host "Idempotent Response:" -ForegroundColor Cyan
  Write-Host ($response2.Content | ConvertFrom-Json | ConvertTo-Json -Depth 5)
  ```
  - You should see the same paymentId as before.
  - Note: This is not perfect.
  For this demo I keyed idempotency on (Idempotency-Key, body-hash) so retries are safe,
  but in a real gateway I would reject a body change for the same key to avoid silent double-charges.
  The normalized body would be hashed to derive the Idempotency-Key to prevent multiple logically identical requests.


## Blockchain / Crypto Demo Mode
A lightweight crypto simulation is wired into the same payment/outbox pipeline to show how blockchain-style flows could be modeled without adding real chain dependencies.

1. Submit a crypto charge (reusing the API key and idempotency headers):
   ```bash
   curl -X POST http://localhost:5023/payments/crypto-charge \
     -H "x-api-key: local-dev" \
     -H "Idempotency-Key: demo-crypto-1" \
     -H "Content-Type: application/json" \
     -d '{
       "amount": 500000,
       "cryptoCurrency": "USDC",
       "network": "Ethereum-Testnet",
       "fromWallet": "0xCafeFood00000000000000000000000000000000",
       "merchantRef": "order-crypto-42"
     }'
   ```
   The API stores a normal `Payment`, creates a `CryptoTransaction` row in `Pending`, and enqueues a `CryptoConfirm` outbox message. The response includes the generated `txHash` so you can track it.

2. Let the worker pick up the new outbox entry. Instead of calling the PSP stub, the worker simulates chain confirmations by marking the crypto transaction confirmed (3 confirmations) and authorizing the payment with the `txHash` as the auth code.

3. Poll the payment or its crypto transaction record:
   ```bash
   curl http://localhost:5023/payments/<paymentId> -H "x-api-key: local-dev"
   curl http://localhost:5023/payments/<paymentId>/crypto -H "x-api-key: local-dev"
   ```
   The crypto endpoint returns network, wallet, confirmations, and timestamps for the simulated transaction.

All crypto charge requests are idempotent just like card charges—the `IdempotencyMiddleware` now protects both `/payments/charge` and `/payments/crypto-charge`.


 
