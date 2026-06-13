# Multi-Currency Wallet & Transfer System

API RESTful desarrollada en .NET 10 para la gestión de billeteras multimoneda (BOB/USD) del BancoSol. Permite crear cuentas, realizar depósitos, retiros y transferencias entre cuentas con conversión de moneda, consultar historial de movimientos y generar reportes consolidados de saldo.

---

## Requisitos Previos

- [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL 14+](https://www.postgresql.org/download/)
- Git (para clonar el repositorio)

---

## Configuración

### 1. Clonar el repositorio

```bash
git clone <url-del-repositorio>
cd Multi-Currency-Wallet-and-Transfer-System/MultiCurrencyWallet
```

### 2. Base de datos

Opción A — **Crear la base de datos manualmente** usando el script SQL:

```bash
psql -U postgres -c "CREATE DATABASE DBSol;"
psql -U postgres -d DBSol -f ../db.md
```

> El archivo `db.md` en la raíz del proyecto contiene el script DDL completo con todas las tablas, restricciones y claves foráneas.

Opción B — **Usar migraciones de EF Core** (requiere que la base de datos exista):

```bash
dotnet ef database update --project Infrastructure --startup-project Api
```

Si no existen migraciones, generarlas:

```bash
dotnet ef migrations add InitialCreate --project Infrastructure --startup-project Api
dotnet ef database update --project Infrastructure --startup-project Api
```

### 3. Configurar la cadena de conexión

La cadena de conexión por defecto en `Api/appsettings.json` apunta a `localhost`:

```json
"ConnectionStrings": {
  "PostgresConnection": "Host=localhost;Port=5432;Database=DBSol;Username=YOUR_USER;Password=YOUR_PASSWORD"
}
```

Ajustar usuario/contraseña según la instalación local de PostgreSQL.

### 4. Configurar clave JWT

En `Api/appsettings.json`, configurar la clave JWT:

```json
"Jwt": {
  "Key": "YOUR_SECRET_KEY",
  "Issuer": "BancoSolWalletApi",
  "Audience": "BancoSolWalletClient",
  "ExpirationMinutes": 60
}
```

Para producción, reemplazar `YOUR_SECRET_KEY` con una clave segura de al menos 32 caracteres.

---

## Ejecutar el Proyecto

```bash
dotnet run --project Api
```

La API estará disponible en:
- **HTTP:** `http://localhost:5257`
- **HTTPS:** `https://localhost:7152`
- **Documentación interactiva:** `http://localhost:5257/scalar/v1`

---

## Ejecutar las Pruebas

```bash
dotnet test
```

Las pruebas usan xUnit + Moq. Cubren 27 escenarios:

| Archivo | Pruebas | Casos cubiertos |
|---|---|---|
| `AccountServiceTests` | 11 | Creación (moneda soportada/no soportada, saldo negativo, cliente inexistente), depósitos (válido, monto cero, cuenta inactiva), retiros (saldo insuficiente, saldo exacto, cuenta bloqueada, monto cero) |
| `TransferServiceTests` | 8 | Idempotencia (clave duplicada), misma cuenta, fondos insuficientes, cuenta origen inactiva, concurrencia (conflicto de versión), misma moneda (sin tasa de cambio), moneda cruzada (con tasa), cuenta origen inexistente |
| `ReportServiceTests` | 8 | Moneda no soportada, cliente inexistente, cuentas BOB en BOB, cuenta USD en BOB (conversión), cuenta BOB en USD (conversión inversa), cuentas mixtas, agregación de créditos/débitos, metadatos del reporte |

---

## Decisiones Técnicas

### Arquitectura: Clean Architecture Onion

Cuatro proyectos con dependencia unidireccional hacia adentro:

```
Api → Application → Domain
Infrastructure → Domain
```

- **Domain:** Modelos puros (`Account`, `Customer`, `Movement`, `Transfer`). Sin dependencias externas.
- **Application:** Contratos (interfaces), DTOs, servicios con lógica de negocio, excepciones personalizadas.
- **Infrastructure:** EF Core (`AppDbContext`), repositorios concretos, servicio de tipo de cambio externo.
- **Api:** Controladores ASP.NET, middleware de excepciones, configuración de DI y middleware pipeline.

### Patrón Repository + Service

Los servicios (`AccountService`, `TransferService`, `ReportService`) encapsulan la lógica de negocio y orquestan operaciones. Los repositorios (`AccountRepository`, `TransferRepository`, `ReportRepository`) abstraen el acceso a datos. La interfaz `IAppDbContext` permite mockear el contexto en pruebas y centralizar la ejecución de transacciones.

### Validaciones

Se usan **Data Annotations** en los DTOs de solicitud (`[Required]`, `[Range]`, `[StringLength]`) para validación en el modelo. El middleware de ASP.NET devuelve automáticamente `400 Bad Request` con los mensajes de error. Las validaciones de negocio adicionales (saldo suficiente, estado de cuenta, moneda soportada) se lanzan como excepciones personalizadas manejadas por `GlobalExceptionMiddleware`.

### Mapeo manual sin AutoMapper

Se usan métodos estáticos `MapTo*` dentro de los servicios para mapear entre entidades de dominio y DTOs. Se evita AutoMapper para mantener la visibilidad explícita de las transformaciones.

### Tipos de dato decimal

Todos los montos usan `decimal` (precisión 18, 2) para evitar errores de redondeo de punto flotante. Las tasas de cambio usan `decimal(18, 8)` para preservar precisión en las conversiones. El redondeo final a 2 decimales se aplica con `Math.Round(valor, 2)` usando el modo predeterminado `MidpointRounding.ToEven` (redondeo bancario).

### Caché con IMemoryCache

- **Historial de movimientos:** Caché con invalidación basada en generación. Cada cuenta tiene un contador `mov_gen_{accountId}` que se incrementa al registrar un nuevo movimiento. La clave de caché incluye este generador, por lo que las entradas anteriores quedan huérfanas automáticamente.
- **Tipo de cambio:** Se cachea por 1 hora con clave `xrate_{from}_{to}`.

### Logging con Serilog

Se registran estructuradamente todas las operaciones críticas (creación de cuenta, depósito, retiro, transferencia, consulta de tipo de cambio, generación de reportes). Los logs se escriben a consola y archivos rotativos diarios en `Api/logs/`.

### Seguridad con JWT

Todos los endpoints (excepto `/api/auth/token`) requieren autenticación mediante token JWT en el header `Authorization: Bearer <token>`. Las credenciales de prueba están hardcodeadas en `AuthController` para propósitos de evaluación.

---

## Estrategia de Concurrencia

**Optimistic Locking (bloqueo optimista)** mediante la columna `version` en la tabla `account`.

### Cómo funciona

1. Cada cuenta tiene un campo `version` (bigint) con valor inicial `1`.
2. En EF Core, la propiedad `Version` está configurada como `IsConcurrencyToken()`.
3. Cuando `TransferService` actualiza el saldo de una cuenta, el `UPDATE` generado por EF Core incluye `WHERE version = @original_version`.
4. Si otra operación concurrente modificó la cuenta entre nuestra `SELECT` y nuestra `UPDATE`, la fila afectada es `0` y EF Core lanza `DbUpdateConcurrencyException`.
5. `TransferService` captura esta excepción y la traduce a `ConcurrencyConflictException`, que el middleware convierte en **HTTP 409 Conflict**.

### ¿Por qué optimistic locking?

- No requiere locks de base de datos ni bloquea filas entre lectura y escritura.
- Escala mejor que `SELECT ... FOR UPDATE` (pesimista) en escenarios de alta concurrencia.
- Es suficiente para la operativa bancaria minorista donde los conflictos son poco frecuentes.
- PostgreSQL nunca deja el saldo negativo porque el `WHERE version = @original` hace que la actualización falle silenciosamente si hay conflicto, y el saldo nunca se modifica en caso de error.

### Manejo adicional de consistencia

Antes de modificar saldos, `TransferService` valida explícitamente `source.Balance >= request.Amount`. Aunque EF Core podría manejar esto con una constraint de check en la base de datos, la validación en la aplicación permite un mensaje de error descriptivo.

---

## Estrategia de Idempotencia

**Doble capa de protección** para evitar duplicación de transferencias.

### Capa 1 — Aplicación (antes de realizar trabajo)

El header `Idempotency-Key: <uuid>` es obligatorio en `POST /api/transfers`. Antes de cualquier validación o modificación, `TransferService` consulta `TransferRepository.GetByIdempotencyKeyAsync(key)`. Si encuentra un registro existente, lanza `DuplicateTransferException` (HTTP 409).

### Capa 2 — Base de datos (protección contra condiciones de carrera)

La tabla `transfer` tiene una **constraint UNIQUE** sobre `idempotency_key`. Si dos solicitudes idénticas llegan simultáneamente y ambas pasan la capa 1, la segunda en hacer `INSERT` recibe un `DbUpdateException` violando la constraint única. `TransferService` captura esta excepción y vuelve a verificar: si el registro ganador existe, retorna `DuplicateTransferException`; si no, relanza el error original.

### ¿Por qué es necesario el doble chequeo?

Sin la capa 1, cada transferencia haría trabajo innecesario (validar cuentas, consultar tasas) antes de descubrir que es duplicada. Sin la capa 2, dos solicitudes paralelas con la misma clave podrían ejecutarse ambas antes de que cualquiera llegue a la verificación inicial.

---

## Estrategia de Resiliencia ante Caída del Proveedor Externo

El `ExchangeRateService` implementa tres capas de defensa:

### Capa 1 — Caché en memoria

La tasa de cambio obtenida se almacena en `IMemoryCache` con un TTL de **1 hora**. Las solicitudes posteriores dentro de esa ventana no llaman al API externo. Esto también reduce la latencia y el número de llamadas salientes.

### Capa 2 — Llamada HTTP al API de HexaRate

El servicio usa un `HttpClient` tipado con **timeout de 10 segundos** (configurado en `Program.cs`). La clase `HexaRateResponse` mapea la respuesta JSON esperada. Si el API responde correctamente, la tasa se cachea y retorna.

### Capa 3 — Tasa de respaldo (fallback)

Si ocurre cualquier excepción en la llamada al API (timeout, error HTTP, respuesta inválida, error de red), se registra una advertencia con Serilog y se retorna una **tasa fija de respaldo**: `6.91 BOB/USD`.

### Garantías

- **El API nunca se cae por el proveedor externo.** Todas las excepciones HTTP se capturan en el bloque `catch` y se manejan gracefulmente.
- **Todas las rutas de código retornan un valor decimal.** No hay caminos donde `GetRateAsync` pueda lanzar una excepción no controlada.
- La tasa de respaldo está documentada como valor referencial y debe actualizarse periódicamente en producción.

---

## Endpoints de la API

| Método | Ruta | Descripción | Autenticación |
|---|---|---|---|
| `POST` | `/api/auth/token` | Obtener token JWT | No |
| `POST` | `/api/accounts` | Crear cuenta | JWT |
| `GET` | `/api/accounts/{id}` | Consultar cuenta por ID | JWT |
| `POST` | `/api/accounts/{id}/deposits` | Realizar depósito | JWT |
| `POST` | `/api/accounts/{id}/withdrawals` | Realizar retiro | JWT |
| `GET` | `/api/accounts/{id}/movements?page=1&pageSize=10` | Historial de movimientos paginado | JWT |
| `POST` | `/api/transfers` | Transferencia entre cuentas | JWT |
| `GET` | `/api/tipo-cambio` | Tipo de cambio USD/BOB vigente | JWT |
| `GET` | `/api/reportes/balance-consolidado?customerId=&startDate=&endDate=¤cy=` | Reporte consolidado de saldo | JWT |


---

## Colección de Postman

La colección de Postman con todos los endpoints preconfigurados se encuentra en el archivo `postman_collection.json` en la raíz del proyecto. Importar en Postman con `File → Import`.
