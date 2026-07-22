# Diseño: juez propio (isolate)

> Estado: **diseño** (sin implementar). Objetivo: correr selectivos y problemas propios
> sin depender de Codeforces.
>
> ⚠️ **Actualización**: los workers pasaron a ser **distribuidos** (varios PCs con WSL2 + isolate),
> no un proceso en el mismo servidor. Eso implica que la cola se consume por **HTTP (Judge API)** y
> aparece la **calibración de velocidad** entre máquinas. El detalle implementable está en
> [`juez-mvp-spec.md`](./juez-mvp-spec.md), que **supersede** la topología de §2 y el despliegue de
> §4 de este documento. El resto (modos, modelo, seguridad, Polygon, roadmap) sigue vigente.

## 0. Decisiones tomadas
- **Sandbox:** `isolate` directo (el de la IOI/CMS: namespaces + cgroups). Máximo control
  (checkers, interactivos) y límites de tiempo/memoria confiables.
- **Problemas:** import de **paquetes Polygon** (ZIP *full*) como vía principal + subida manual.
- **Despliegue:** **todo en un servidor** (ver §4). Debe ser Linux.
- **Un solo motor de ejecución para todo.** El juez tiene dos **modos** (ver §1.5):
  - `judge`: corre contra el set de tests de un problema → veredicto (AC/WA/…). Para selectivos,
    training, contests.
  - `run`: corre **una vez** con el stdin del usuario → devuelve stdout/stderr/tiempo. **Es lo que
    usa el runner de código del Apunte**: subir código como a un problema, pero que te entregue el
    output en vez de un veredicto.
- **Alcance MVP:** C++ + ambos modos (`judge` batch con checker por tokens; `run` para el Apunte),
  veredictos ICPC + penalización, alimentando los scoreboards existentes.

---

## 0.5 Alternativas evaluadas (por qué no adoptamos un juez existente)

**CMS — Contest Management System de la IOI** (cms-dev.github.io). Verificado en su doc:
usa **`isolate`** como sandbox (setuid + cgroup v2), está escrito en **Python** y requiere
**su propio PostgreSQL** (usa Large Objects). Es un **sistema distribuido de servicios**
(EvaluationService, Worker, ScoringService, ContestWebServer, AdminWebServer, RankingWebServer…).

- ✅ A favor: probado en la IOI; subtareas/puntaje parcial e interactivos **nativos**; cero código
  de juzgado que escribir.
- ❌ En contra para nuestro caso: es una **aplicación completa, no un servicio embebible** — no
  expone una API "mándame código, dame veredicto". El competidor usaría **la UI de CMS**, no la
  plataforma; los solves no alimentarían `UserProblemStatus`/entrenamientos/rankings sin sincronizar
  contra su DB; **el modo `run` del Apunte no existe** en su modelo; y suma varios servicios + una
  segunda base de datos al servidor único.

**DOMjudge**: más on-target que CMS para selectivos **estilo ICPC** (y ya parseamos sus `.dat`),
pero tiene el mismo problema de fondo: es una aplicación aparte, no un motor embebible.

**Decisión: construir delgado sobre `isolate`.** La parte peligrosa y difícil (el sandbox) es
**exactamente la misma que usa CMS**; nosotros solo escribimos la orquestación (cola, loop de
tests, checker, agregación de veredicto), que es acotada y ya está diseñada. A cambio conservamos
lo que nos diferencia: submissions en nuestra UI, solves que alimentan el resto de la plataforma, y
el modo `run` del Apunte gratis.

**Salida de emergencia:** para un contest de alto riesgo siempre podemos correrlo en CMS/DOMjudge e
**importar el scoreboard**, que es un músculo que la plataforma ya tiene (importadores BOCA/cphof).
Y CMS sigue siendo una **referencia excelente** para copiar decisiones de diseño (tipos de tarea,
modelo de subtareas, cómo invoca isolate).

---

## 1.5 Modos de ejecución (juez y Apunte, un mismo pipeline)

Todo pasa por la misma cola, el mismo worker y el mismo `isolate`. Lo único que cambia es qué se
hace **después** de compilar y ejecutar:

| | modo `judge` | modo `run` (Apunte) |
|---|---|---|
| Entrada | los tests del problema | el **stdin** que escribe el lector |
| Ejecuciones | 1 por test | 1 sola |
| Salida al usuario | veredicto + penalización + detalle por test | **stdout + stderr + exit + tiempo/memoria** |
| Checker | sí (tokens/testlib) | no |
| Persistencia | `Submission` permanente (cuenta para el scoreboard) | efímera (registro corto, se puede purgar) |
| Quién dispara | participante en un contest/training | lector del Apunte (botón "Run") |
| Límites | los del problema | fijos y **chicos** (p. ej. 5 s, 256 MB, salida 64 KB) |
| Prioridad | **alta** (crítico en contest) | best-effort (no debe robar cómputo al judging) |

Consecuencia clave: **no hay dos sistemas**. El Apunte "sube código" igual que un problema; el
backend lo trata como una `Submission` de modo `run` y devuelve el output. Menos infra, un solo
lugar que endurecer.

---

## 1. Por qué un solo servidor alcanza (y sigue siendo seguro)

La idea original de "separar el juez en otra máquina" era *defensa en profundidad*. Pero la
**frontera de seguridad real no es la máquina: es `isolate`**. Por lo tanto, un solo servidor
bien configurado es suficiente:

1. **El código no confiable nunca corre como el worker.** Corre **dentro de una caja isolate**,
   con su propio namespace de red, PID, mount y usuario. El worker (tu código, confiable) solo
   orquesta.
2. **La caja no tiene red por defecto.** `isolate` no comparte red salvo que pases `--share-net`
   (que **no** usamos). Sin interfaz de red, el código enviado **no puede alcanzar
   `localhost:5432`** aunque Postgres esté en el mismo servidor. Este es el punto clave: la
   co-ubicación no expone la DB.
3. **El worker usa un rol de Postgres restringido** (solo `submissions` / resultados / lectura de
   problemas), nunca el usuario de la app ni sus secretos. Aunque el worker fuera comprometido,
   su alcance en la DB es mínimo.
4. **Los secretos de la app no se montan** en las cajas isolate (solo se monta el binario del
   participante, los archivos del test y el checker).

Conclusión: la separación por máquina aporta poco frente a lo que ya da `isolate` + red apagada +
rol de DB restringido. **Un servidor Linux basta.**

### Lo que sí hay que cuidar en un solo servidor: la *equidad de tiempos*
El juez compite por CPU con la API y Postgres. Para que los TLE sean justos:
- **Reservar núcleos para el juez** con `cpuset` (cgroup): p. ej. en un server de 8 vCPU, cores
  0–5 para app+DB, cores 6–7 exclusivos para judging.
- **Concurrencia acotada** = núcleos reservados (1–2 juzgados simultáneos). El resto encola.
- **Medir CPU-time, no wall-time**, para el veredicto de TLE (menos sensible a la carga del host).
- Ideal en prod: fijar frecuencia de CPU (governor `performance`) para tiempos estables.

Con esto, un servidor moderno (**~4–8 vCPU, 8–16 GB RAM**) corre un selectivo de comunidad sin
problema: la carga de submissions es a ráfagas y acotada.

---

## 2. Topología (un servidor)

```
┌──────────────────────── Servidor Linux ────────────────────────┐
│                                                                 │
│  Docker Compose ───────────────┐     Host (systemd)             │
│   · api (.NET)                 │      · judge-worker            │
│   · postgres  ── localhost ────┼───────  (rol DB restringido)   │
│   · web (static React)         │            │                   │
│                                │            ▼                   │
│                                │        isolate  (cajas: sin    │
│  cola = tabla en Postgres      │        red, cgroups, límites)  │
│  (FOR UPDATE SKIP LOCKED)      │            │                   │
│                                │            ▼                   │
│  judge-data/  (volumen) ◄──────┴──────  tests + checker + bin   │
└─────────────────────────────────────────────────────────────────┘
```

- **API + Postgres + web** en Docker Compose (como hoy).
- **judge-worker + isolate corren en el host** (servicio `systemd`), no en Docker. Razón: isolate
  necesita cgroups/`setuid` y es mucho más confiable directo en el host que en un contenedor
  privilegiado (ver §4).
- **Cola**: tabla `submissions` en Postgres con `SELECT … FOR UPDATE SKIP LOCKED`. Cero infra
  extra; escalas subiendo el nº de slots del worker.
- **Test data**: en un directorio del host (`judge-data/{problemId}/…`) montado también en el
  contenedor de la API (para servir enunciados/gestionar), **no** en Postgres.

---

## 3. Flujo de ejecución (compartido)

**Común a ambos modos** (lo hace el worker tras tomar el trabajo de la cola):
1. Marca `judging`. **Compila** dentro de isolate (sin red, límites generosos). Falla → **CE**, fin.
   Cachea el binario.

**Modo `judge`** (por cada test del problema):
2. `isolate --run` con `--time` (CPU), `--wall-time`, `--mem` (cgroup), `--processes`, `--fsize`,
   `stdin={i}.in`, `--meta`. Lee el meta:
   - `status=TO` → **TLE**, `SG`/`RE` → **RE**, salida > límite → **OLE**, memoria → **MLE**.
   - Si corrió OK → corre el **checker** en otra caja con `(input, salida, answer)` → **AC/WA**.
3. Agrega veredicto (primer no-AC en ICPC; suma de subtareas en OCI) + penalización → guarda
   `Submission` + `SubmissionTestResult[]` → `status=done`. Front hace *polling* cada 1–2 s.

**Modo `run` (Apunte)** (una sola ejecución):
2. `isolate --run` con el **stdin del usuario** y los límites fijos chicos (§1.5). Lee el meta.
3. Devuelve `{stdout, stderr, exitCode, timeMs, memoryKb, truncated?}` (recorta stdout/stderr al
   límite de salida). Sin checker, sin veredicto. Front muestra el output.

---

## 4. Despliegue en el servidor

**Recomendado — worker en el host, app en Docker:**
- Instalar `isolate` en el host (paquete o compilado; requiere cgroup v2 delegado + binario
  `setuid root`).
- `judge-worker` como servicio `systemd`, con `AllowedCPUs=6-7` (cpuset) y el rol de DB
  restringido; se conecta a `localhost:5432` (Postgres del compose, con puerto publicado o red host).
- API/Postgres/web siguen en `docker compose` como hoy.
- Ventaja: isolate "simplemente funciona" (cgroups del host), sin pelear con contenedores
  privilegiados.

**Alternativa — todo dockerizado (worker en contenedor privilegiado):**
- `judge-worker` como servicio más en el compose, con `privileged: true`, `/sys/fs/cgroup`
  montado y delegación de cgroup v2. Funciona (DOMjudge lo hace) pero es más frágil de configurar.
- Elegir esta solo si quieres un `docker compose up` único; si no, la opción host es más simple.

**Desarrollo (Windows):** isolate **no** corre en Docker Desktop/Windows de forma confiable.
Para desarrollar el juez: una **VM Linux** o **WSL2** (cgroup v2), o probar contra el servidor de
staging. La lógica de orquestación (cola, parsing de meta, checker) sí se puede desarrollar con un
*runner* de reemplazo local que no aísle (solo para tests unitarios, nunca en prod).

---

## 5. Modelo de datos (entidades nuevas)
Reusa `Problem` / `Contest` / `ContestProblem` / `UserProblemStatus` y agrega:

- **`Submission`** (unifica ambos modos):
  - `id`, `mode` (`judge`/`run`), `userId?` (opcional en `run` anónimo), `language`, `sourceCode`.
  - `problemId?` (obligatorio en `judge`; opcional en `run`), `contestId?`.
  - `stdin?` (solo `run`).
  - `status` (`queued`/`compiling`/`running`/`done`/`error`).
  - **judge:** `verdict`, `score`, `penalty`, `cpuTimeMs`, `wallTimeMs`, `memoryKb`.
  - **run:** `stdout`, `stderr`, `exitCode`, `timeMs`, `memoryKb`, `truncated`.
  - `priority` (judge > run), `createdAt`, `judgedAt`.
- **`SubmissionTestResult`** (solo `judge`): submission, testIndex, verdict, timeMs, memoryKb.
- **`JudgeProblem`** (config por problema): timeLimitMs, memoryLimitKb, checkerType
  (`tokens`/`testlib`), checkerSource, outputLimitKb, scoringMode (`icpc`/`subtasks`).
- **`ProblemTest`**: índice, grupo/subtarea, puntos, rutas de input/answer (los archivos van en
  `judge-data/`, no en la fila).

Las `run` se pueden purgar por antigüedad (TTL) — no cuentan para nada permanente.

---

## 5.5 Endpoints (API surface)

**Ejecución**
- `POST /api/submissions` — modo `judge`. Body `{problemId, contestId?, language, source}`. Auth +
  validación de ventana del contest. → `{submissionId}`.
- `POST /api/run` — modo `run` (Apunte / custom invocation). Body `{language, source, stdin?}`.
  Auth (o anónimo con rate-limit fuerte, §12). → `{runId}`.
- `GET /api/submissions/{id}` — estado + resultado (veredicto o output según modo). El front lo
  *pollea* hasta `done`.
- `GET /api/languages` — lenguajes disponibles (para el selector del editor).

**Admin (problemas)**
- `POST /api/admin/problems/import-polygon` — sube ZIP *full* de Polygon → crea `JudgeProblem` +
  tests + checker.
- `POST/PATCH /api/admin/problems/{id}/judge` — editar límites/checker/scoring.
- `PUT /api/admin/problems/{id}/tests` — subir/reemplazar tests manualmente.
- `POST /api/admin/submissions/{id}/rejudge` — re-juzgar (fase 2).

## 5.6 Layout de `judge-data/`
```
judge-data/
  problems/{problemId}/
    meta.json              # límites, checkerType, scoring (espejo de JudgeProblem)
    tests/                 # 01.in 01.a 02.in 02.a …   (o por grupo/subtarea)
    checker/               # check.cpp + testlib.h  (o "tokens")
    checker.bin            # checker compilado (cache)
  cache/bin/{submissionId} # binario del participante (efímero)
```
Fuera de Postgres (pesa). Montado en la API (gestión/enunciados) y accesible por el worker.

---

## 6. isolate: cómo lo usamos
- Un **box por slot de concurrencia** (`--box-id N`); N slots = N cores reservados.
- Ciclo por ejecución: `--init` → `--run` (`--meta`, `--no-default-dirs`, `--env` mínimo, montar
  solo lo necesario, **sin `--share-net`**) → leer meta → `--cleanup`.
- **Compilar también aislado** (los compiladores comen memoria y son superficie de ataque);
  cachear el binario para la fase de ejecución.
- El **checker testlib** también corre dentro de isolate.
- Meta entrega: `time` (CPU), `time-wall`, `max-rss` (memoria), `exitcode`, `status`, `killed`.

---

## 7. Preparación de problemas (Polygon)
- **Import de paquete Polygon** (ZIP *full*): parsea `problem.xml` (límites, checker, grupos/puntos)
  → copia `tests/*.in` y `*.a` a `judge-data/{problemId}/` → guarda/compila `check.cpp` + `testlib.h`.
  Es como importan DOMjudge/Kattis. Usar el paquete **full** (trae respuestas `.a` ya generadas).
- **Subida manual** (para problemas propios nuevos): UI admin para tests + checker + límites.
- **Checkers**: `tokens` (comparación por tokens, cubre la mayoría) + `testlib` (special judge)
  para respuestas no únicas. Interactivos/subtareas: fase 2+.

---

## 8. Veredictos y scoring
- Veredictos: **CE, AC, WA, TLE (CPU/wall), MLE, RE, OLE, IE**.
- Scoring **ICPC** (AC/no + penalización = minutos + 20×intentos-fallidos) para selectivos →
  encaja directo con `IcpcStandingsTable`.
- Scoring **subtareas/parcial** (OCI) en fase 3 → encaja con `OciStandingsTable`.

---

## 9. Integración con lo existente
- **Selectivo/contest** = un `Contest` cuyos problemas tienen `JudgeProblem`. Las submissions
  propias (modo `judge`) llenan veredictos/`UserProblemStatus` → los templates
  `ContestStandingsPanel` / `IcpcStandingsTable` **ya lo pintan**. Cambia la fuente, no la UI.
- **Apunte** = las secciones de código llaman a `POST /api/run` (modo `run`) y muestran el output.
  El editor del Apunte es un cliente más del mismo motor; no necesita nada especial en el backend.
- Los gyms de CF siguen para training casual; los selectivos migran al juez propio.

---

## 10. Seguridad (checklist)
- Cajas isolate **sin red** (nunca `--share-net`) → la DB en localhost es inalcanzable desde el código enviado.
- Worker con **rol de DB restringido**; sin secretos de la app montados en las cajas.
- Límites por run: **CPU + wall + memoria + procesos + tamaño de salida**; filesystem efímero.
- Límites también al **compilador**; validar tamaño de la fuente; **rate-limit** de submissions.
- `cpuset` reservado para el juez (equidad y aislamiento de recursos).

### Específico del modo `run` (Apunte) — es la mayor superficie de abuso
- Lo dispara cualquier lector con un botón, no un participante en un contest → **rate-limit fuerte**
  por usuario/IP, y **auth o captcha** si el Apunte es público (decisión en §12).
- Límites **fijos y chicos** (5 s, 256 MB, salida 64 KB), independientes de cualquier problema.
- **Prioridad baja / presupuesto de cómputo separado**: los `run` **no deben robar cores al
  judging** durante un contest. Opciones: cola/priority separada, o un pool chico de slots
  dedicado a `run` distinto de los slots de `judge`.
- Purga por TTL de las filas `run` (no son historial).

---

## 11. Roadmap
1. **MVP**: C++, **ambos modos** (`judge` batch con checker por tokens + `run` para el Apunte),
   worker+isolate en el host, cola Postgres con prioridad, veredictos ICPC → corre un selectivo y
   el runner del Apunte funciona. (tests: subida manual o script.)
2. **Fase 2**: **import Polygon** (ZIP), checker testlib, Python, UI admin de problemas, rejudge,
   detalle por test.
3. **Fase 3**: subtareas/parcial (OCI), interactivos, varios slots, SSE en vivo (veredicto y output
   en tiempo real en vez de polling).

---

## 12. Decisiones abiertas / riesgos
- **Servidor de prod**: ¿ya es Linux y con cuántos vCPU/RAM? (define cores reservados y concurrencia).
- **Worker: host-systemd vs contenedor privilegiado** (recomiendo host por simplicidad de isolate).
- **Aislamiento worker↔DB**: rol restringido (MVP) → *judge-API* interno (a mediano plazo) si se
  quiere que el worker ni siquiera hable con Postgres.
- **`run` anónimo vs con login**: ¿el runner del Apunte lo puede usar cualquiera (público) o solo
  usuarios logueados? Define el rate-limit y si hace falta captcha.
- **Prioridad run↔judge**: cola con prioridad simple, o pool de slots separado para `run`.
- **Dev sin Linux**: definir VM/WSL2 vs staging para probar el juzgado real.
