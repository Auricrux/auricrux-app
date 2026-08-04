# Live 3B TRUE God train protection

**Policy:** `auricrux/system/live_3b_train_protection_policy.json`  
**Assert:** `scripts/Assert-Live3bTrainProtection.ps1` → `LIVE_3B_TRAIN_PROTECTION_OK`  
**Manifest:** `trueGodRun.status = running-do-not-interrupt` (PID recorded; **not** probed by default)

## Hard rules

Do **not** interrupt the live train.  
Do **not** restart the live train.  
Do **not** pause the live train.  
Do **not** move the live train.  
Do **not** kill, resume, renice, or cgroup-starve the live train.  
Do **not** “optimize” the live train.  
Do **not** merge/export on the train GPU (`CUDA_VISIBLE_DEVICES` must be empty for any CPU export).  
Do **not** start a competing train on the same GPU while status is `running-do-not-interrupt`.  
Do **not** delete the sole live checkpoint / wipe the train run root.  
Do **not** tamper with the token factory feeding the live run.

**Only protect it.**

## Path classes

| Class | Paths | Train contact |
|-------|-------|---------------|
| SAFE / isolated | Suite runner, deployment safety gate, ollama init, warm, cutover build, Azure zip deploy, product clobber assert | **None** — product host / local only |
| READ-ONLY OK | Manifest reads, ops context, `ps -p <pid>` probes | Observation only |
| CPU-ONLY guarded | `stage-and-cpu-export-checkpoint.ps1` | May stage-copy checkpoint + CPU merge with `CUDA_VISIBLE_DEVICES=` empty — **never** kill PID; **never** use train GPU |
| FORBIDDEN | kill/pkill, `az vm stop/deallocate`, GPU reset, GPU merge on train host | Blocked by policy + assert |

## Starvation notes (protect, don’t “fix”)

- CPU export on the train **host** can compete for **RAM/disk I/O** even with empty CUDA — run only when needed; prefer export-clean host when available. Do not starve the live job.
- Product warm/suite/cutover use the **GCE product** Ollama/web stack — they do not SSH to the train VM.
- Do not fill `/mnt/auricrux-eod` to ENOSPC (checkpoint loss risk).

## Operator check

```powershell
cd C:\Users\MichaelBartholomew\source\fca-real-product\auricrux-app
.\scripts\Assert-Live3bTrainProtection.ps1
```

Assert is **static only** — it does not SSH to the train host and does not touch the PID.

## Related

- [PRODUCT_MODEL_CLOBBER_PROTECTION.md](./PRODUCT_MODEL_CLOBBER_PROTECTION.md) (product tag `auricrux-fca`)
- [OLLAMA_INIT_SAFE_UNSAFE_PATHS.md](./OLLAMA_INIT_SAFE_UNSAFE_PATHS.md)
- [AURICRUX_STATUS_TRUTH_2026-08-03.md](./AURICRUX_STATUS_TRUTH_2026-08-03.md)
- [AURICRUX_LONG_TRAIN_RECOVERY_PROCEDURE.md](./AURICRUX_LONG_TRAIN_RECOVERY_PROCEDURE.md) (extended-duration resume / disk / corruption — never while do-not-interrupt)
- [AURICRUX_TRAIN_SURVIVABILITY_AUDIT_2026-08-03.md](./AURICRUX_TRAIN_SURVIVABILITY_AUDIT_2026-08-03.md)
