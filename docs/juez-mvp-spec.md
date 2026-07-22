# Spec implementable — Juez propio (MVP)

> Continuación de [`juez-propio-diseno.md`](./juez-propio-diseno.md) (el *qué* y el *por qué*).
> Este documento es el *cómo*: tablas, endpoints, comandos concretos y orden de trabajo.
>
> **Cambio respecto al diseño original:** los workers ahora son **distribuidos** (varios PCs), no
> un proceso en el mismo servidor. Eso obliga a que la cola se consuma por **HTTP (Judge API)** y
> no por acceso directo a Postgres. Ver §2 y §9.

---

## 1. Alcance del MVP

**Entra:**
- Lenguaje **C++** (g++). Recetas extensibles a Python después.
- Modo **`judge`**: problemas *batch* (stdin → stdout), checker **por tokens**, veredicto ICPC.
- Modo **`run`**: una ejecución con stdin del usuario → stdout/stderr (el runner del Apunte).
- **Workers distribuidos** en varios PCs (WSL2 + isolate), con calibración de velocidad.
- Cola con prioridad (`judge` > `run`), leases y reintentos.

**No entra (fases siguientes):** checker testlib, subtareas/puntaje parcial, interactivos,
import Polygon, rejudge masivo, SSE en vivo.

---

## 2. Arquitectura (coordinador + workers distribuidos)

```
                    ┌──────────── Servidor / PC coordinador ────────────┐
   Navegador ─HTTP─▶ │  API .NET  ──  Postgres  ──  judge-data/          │
                    └───────▲───────────────────────────────┬──────────┘
                            │ Judge API (HTTP + token)      │ bundles (zip)
              ┌─────────────┼───────────────┬───────────────┘
              │             │               │
        ┌─────▼─────┐ ┌─────▼─────┐  ┌──────▼────┐
        │ Worker PC │ │ Worker PC │  │ Worker PC │   cada uno: WSL2 + isolate
        │  2 slots  │ │  4 slots  │  │  1 slot   │   + caché local de tests
        └───────────┘ └───────────┘  └───────────┘
```

Reglas que se derivan de tener workers en varios PCs:

1. **Postgres nunca se expone.** El worker solo habla HTTPS con la Judge API (§5.2).
2. **Los tests viajan al worker** como *bundle* zip cacheado localmente (§6).
3. **Los tiempos no son comparables entre máquinas** → calibración obligatoria (§8).
4. **Un worker es un operador de confianza**: ve la test data. Tokens revocables (§9).

---

## 3. Modelo de datos (EF Core)

### 3.1 `Submission` (unifica ambos modos)
| Campo | Tipo | Notas |
|---|---|---|
| `Id` | Guid | PK |
| `Mode` | enum `judge`\|`run` | |
| `UserId` | Guid? | null si `run` anónimo |
| `ProblemId` | Guid? | requerido si `judge` |
| `ContestId` | Guid? | |
| `Language` | string | `cpp` (clave de la receta, §7) |
| `SourceCode` | text | límite 64 KB (validado en API) |
| `Stdin` | text? | solo `run`, límite 64 KB |
| `Status` | enum | `queued`\|`claimed`\|`running`\|`done`\|`error` |
| `Priority` | int | 0 = judge, 10 = run (menor primero) |
| `Verdict` | string? | `AC`,`WA`,`TLE`,`MLE`,`RE`,`CE`,`OLE`,`IE` |
| `CpuTimeMs`,`WallTimeMs`,`MemoryKb` | int? | máximo entre tests |
| `Penalty` | int? | ICPC (solo judge) |
| `Stdout`,`Stderr` | text? | solo `run` (truncados a 64 KB) |
| `ExitCode` | int? | solo `run` |
| `Truncated` | bool | solo `run` |
| `CompileOutput` | text? | mensaje de CE |
| `WorkerId` | Guid? | quién lo juzgó (auditoría) |
| `LeaseUntil` | timestamptz? | para requeue si el worker muere |
| `Attempts` | int | reintentos |
| `CreatedAtUtc`,`JudgedAtUtc` | timestamptz | |

Índice clave para la cola: `(Status, Priority, CreatedAtUtc)`.

### 3.2 `SubmissionTestResult` (solo `judge`)
`Id`, `SubmissionId`, `TestIndex`, `Verdict`, `TimeMs`, `MemoryKb`.

### 3.3 `JudgeProblem` (config de juez por `Problem`)
`ProblemId` (PK/FK), `TimeLimitMs`, `MemoryLimitKb`, `OutputLimitKb`,
`CheckerType` (`tokens` en MVP), `ScoringMode` (`icpc`), `TestsVersion` (string/hash — invalida
la caché de los workers), `TestCount`.

### 3.4 `JudgeWorker`
`Id`, `Name`, `TokenHash`, `Slots` (concurrencia), `SpeedFactor` (double, §8),
`TrustedForContest` (bool), `Enabled` (bool), `LastSeenAtUtc`, `Version`.

---

## 4. Estados y ciclo de vida

```
queued ──claim──▶ claimed ──ack──▶ running ──result──▶ done
   ▲                  │                │
   └──── lease vencido / worker caído ──┘   (Attempts++, si Attempts>3 → error/IE)
```
Un job vencido (`LeaseUntil < now`) vuelve a `queued` por un barrido periódico del coordinador.

---

## 5. Contrato de API

### 5.1 Usuario / front
| Método | Ruta | Body | Respuesta |
|---|---|---|---|
| POST | `/api/submissions` | `{problemId, contestId?, language, source}` | `{submissionId}` |
| POST | `/api/run` | `{language, source, stdin?}` | `{submissionId}` |
| GET | `/api/submissions/{id}` | — | estado + veredicto **o** output según `mode` |
| GET | `/api/languages` | — | `[{id:"cpp", label:"C++20"}]` |

Validaciones: tamaño de fuente/stdin, ventana del contest, **rate-limit** (§9).

### 5.2 Judge API (workers) — auth: `Authorization: Bearer <worker-token>`
| Método | Ruta | Body | Respuesta |
|---|---|---|---|
| POST | `/api/judge/hello` | `{name, slots, version, speedFactor}` | `{workerId, leaseSeconds}` |
| POST | `/api/judge/claim` | `{capacity}` | `[{jobId, mode, language, source, stdin?, problem:{id,testsVersion,timeLimitMs,memoryLimitKb,outputLimitKb,testCount,checkerType}}]` |
| POST | `/api/judge/jobs/{id}/heartbeat` | — | `{leaseUntil}` |
| POST | `/api/judge/jobs/{id}/result` | ver §5.3 | `204` |
| GET | `/api/judge/problems/{id}/bundle?v={testsVersion}` | — | `application/zip` |

`claim` usa `UPDATE … WHERE Id IN (SELECT … FOR UPDATE SKIP LOCKED)` para entregar N jobs
atómicamente y setear `Status=claimed`, `WorkerId`, `LeaseUntil`.

### 5.3 Payload de resultado
```jsonc
// modo judge
{ "verdict":"WA", "cpuTimeMs":120, "wallTimeMs":140, "memoryKb":20480,
  "compileOutput": null,
  "tests":[{"index":1,"verdict":"AC","timeMs":10,"memoryKb":1800}, ...] }

// modo run
{ "verdict":"OK", "stdout":"...", "stderr":"...", "exitCode":0,
  "cpuTimeMs":8, "memoryKb":1500, "truncated":false }
```

### 5.4 Admin
`POST /api/admin/problems/{id}/tests` (sube zip de tests → sube `TestsVersion`),
`PATCH /api/admin/problems/{id}/judge` (límites), `GET/POST /api/admin/judge/workers` (alta,
token, enable/disable), `POST /api/admin/submissions/{id}/rejudge`.

---

## 6. Test data y bundles

**En el coordinador** (`judge-data/`, ya montado en el volumen de la API):
```
judge-data/problems/{problemId}/
  meta.json          # límites, checkerType, testCount, testsVersion
  tests/01.in 01.a 02.in 02.a …
```

**Bundle**: `GET /api/judge/problems/{id}/bundle?v=…` devuelve ese directorio como zip.

**Caché del worker**: `~/.judge-cache/{problemId}/{testsVersion}/`. Si el `testsVersion` del job no
está en caché, descarga el bundle. Cambiar tests ⇒ nuevo `TestsVersion` ⇒ invalidación automática.

---

## 7. Recetas de lenguaje

```jsonc
"cpp": {
  "sourceFile": "sol.cpp",
  "compile": ["/usr/bin/g++", "-O2", "-std=c++20", "-o", "sol", "sol.cpp"],
  "compileTimeMs": 15000, "compileMemoryKb": 1048576,
  "run": ["./sol"],
  "timeFactor": 1.0
}
// Python (fase 2): sin compile, run ["/usr/bin/python3","sol.py"], timeFactor 3.0
```

---

## 8. Calibración entre PCs (⚠️ crítico con workers distribuidos)

Distintos PCs ⇒ **el mismo programa tarda distinto** ⇒ un TLE dependería de qué worker tocó. Sin
resolver esto, el juzgado no es justo.

1. **Benchmark al registrarse**: el worker corre un programa fijo de referencia (bucle
   determinista, ~1 s en la máquina de referencia) y reporta `speedFactor = tiempoMedido /
   tiempoReferencia`.
2. **Límite efectivo**: `effectiveTimeLimit = timeLimitMs × speedFactor` (worker lento ⇒ más
   tiempo). El veredicto se decide contra el límite efectivo; se **reporta el tiempo normalizado**
   (`cpuTimeMs / speedFactor`) para que los números sean comparables entre máquinas.
3. **Tolerancia**: si `speedFactor` sale fuera de rango (p. ej. >2.5), el worker no puede marcarse
   `TrustedForContest`.
4. **Contest**: los jobs de un contest se asignan **solo a workers `TrustedForContest`**. Los `run`
   del Apunte y el training pueden ir a cualquiera.
5. Se guarda `WorkerId` en cada submission para poder **re-juzgar** en un worker de referencia si
   algo se disputa.

> Recomendación: para un selectivo real, calibrar y usar un conjunto homogéneo. WSL2 sobre
> escritorios es aceptable para training/apunte; para competencia crítica, mejor máquinas dedicadas.

---

## 9. Seguridad

- **Cajas isolate sin red** (nunca `--share-net`) → el código enviado no alcanza nada.
- **Workers hablan solo HTTP con la Judge API**; Postgres jamás expuesto a la red.
- **Token por worker** (guardar solo el hash), revocable, `Enabled=false` lo saca de la cola.
- **La test data viaja a máquinas ajenas** ⇒ los operadores de workers deben ser de confianza. Para
  un contest en vivo con problemas secretos, usar solo workers propios.
- Límites por ejecución: CPU + wall + memoria + procesos + tamaño de salida; FS efímero.
- Límites también al **compilador**; validar tamaño de la fuente.
- **Rate-limit del modo `run`** (el botón del Apunte lo aprieta cualquiera): por usuario/IP, y
  presupuesto de slots separado para que no le robe cómputo al judging.

---

## 10. Ejecución con isolate (comandos concretos)

> Verificar flags contra la versión instalada (`isolate --help`): isolate v1 y v2 difieren en el
> manejo de cgroups (`--cg`, `--cg-mem` vs `--mem`).

```bash
BOX=$(isolate --cg --box-id=$N --init)          # → /var/local/lib/isolate/$N
cp sol.cpp $BOX/box/

# Compilar (aislado también)
isolate --cg --box-id=$N --run \
  --meta=$META --time=15 --wall-time=20 --cg-mem=1048576 --processes=8 \
  --stderr=compile.err --env=PATH=/usr/bin:/bin \
  -- /usr/bin/g++ -O2 -std=c++20 -o sol sol.cpp

# Ejecutar un test
isolate --cg --box-id=$N --run \
  --meta=$META \
  --time=$((TL/1000)) --wall-time=$((3*TL/1000)) --extra-time=0.5 \
  --cg-mem=$MEM_KB --processes=1 --fsize=$OUT_KB \
  --stdin=01.in --stdout=out.txt --stderr=err.txt \
  --no-default-dirs --dir=/box=box:rw --dir=/etc --env=PATH=/usr/bin:/bin \
  -- ./sol

isolate --cg --box-id=$N --cleanup
```

**Mapa `meta` → veredicto:**
| meta | Veredicto |
|---|---|
| sin `status`, `exitcode=0` | corrió OK → decide el checker (**AC**/**WA**) |
| `status=TO` | **TLE** |
| `status=SG` y `cg-mem`/`max-rss` ≥ límite | **MLE** |
| `status=SG` (otro) | **RE** (señal) |
| `status=RE` (exit ≠ 0) | **RE** |
| salida > `fsize` | **OLE** |
| `status=XX` | **IE** (reintentar en otro worker) |

Campos útiles del meta: `time`, `time-wall`, `max-rss`, `cg-mem`, `exitcode`, `exitsig`, `status`,
`killed`, `message`.

**Checker por tokens (MVP)**: comparar `out.txt` vs `NN.a` ignorando espacios/saltos al final y
colapsando espacios; comparación token a token. (testlib llega en fase 2.)

**Corte temprano (ICPC)**: en `judge`, al primer test no-AC se detiene y se reporta ese veredicto.

---

## 11. Worker: bucle principal

```
loop:
  jobs = POST /api/judge/claim {capacity: slotsLibres}
  si vacío: dormir 1–2 s; continuar
  por cada job (en su slot):
    asegurar bundle en caché (descargar si falta testsVersion)
    compilar en isolate → si falla: reportar CE, siguiente
    si mode=run:  1 ejecución con stdin del job → stdout/stderr/tiempo
    si mode=judge: por cada test → ejecutar → checker → acumular; cortar al primer no-AC
    POST /api/judge/jobs/{id}/result
  heartbeat cada 10 s por job en curso
```
Config del worker: `apiUrl`, `token`, `slots`, `boxIdBase` (para no chocar `--box-id` entre
procesos), `cacheDir`.

---

## 12. Puesta en marcha en WSL2

1. **Distro**: Ubuntu 22.04/24.04 en WSL2. Verificar cgroup v2:
   `mount | grep cgroup2` (debe existir `/sys/fs/cgroup`).
2. **systemd en WSL** (para correr el worker como servicio) — `/etc/wsl.conf`:
   ```ini
   [boot]
   systemd=true
   ```
   luego `wsl --shutdown` y reabrir.
3. **Instalar isolate**: dependencias (`build-essential`, `libcap-dev`, `pkg-config`), compilar el
   repo `ioi/isolate`, `make install`. Queda **setuid root**; los usuarios del grupo `isolate`
   pueden invocarlo.
4. **Sanity check**:
   ```bash
   isolate --cg --box-id=0 --init
   isolate --cg --box-id=0 --run --meta=/tmp/m.txt --time=1 -- /bin/echo hola
   cat /tmp/m.txt && isolate --cg --box-id=0 --cleanup
   ```
5. **Compiladores**: `g++` (y `python3` en fase 2).
6. **Worker**: correr con `apiUrl` apuntando al coordinador y su token.

> ⚠️ WSL2 es una VM: los tiempos son **menos estables** que en bare metal. Sirve para desarrollo,
> apunte y training; para un selectivo crítico, preferir máquinas Linux dedicadas y calibradas (§8).

---

## 13. Orden de implementación

1. **Migración + entidades** (`Submission`, `SubmissionTestResult`, `JudgeProblem`, `JudgeWorker`).
2. **API de usuario**: `POST /api/run`, `GET /api/submissions/{id}` (sin worker todavía).
3. **Judge API**: `hello`, `claim` (SKIP LOCKED + lease), `heartbeat`, `result`, `bundle`.
4. **Worker** (proyecto aparte, corre en WSL2): config, caché de bundles, isolate wrapper, parseo
   de meta, checker por tokens, bucle de claim. Probar **solo modo `run`** de punta a punta.
5. **Modo `judge`**: subida de tests (admin), loop de tests, veredicto ICPC, corte temprano.
6. **Calibración** (`speedFactor`, `TrustedForContest`) + barrido de leases vencidos.
7. **Front**: bloque de "ejecutar código" (polling) y página de submissions/veredicto.
8. **Integración**: veredictos → `UserProblemStatus` → standings existentes → correr un selectivo.
