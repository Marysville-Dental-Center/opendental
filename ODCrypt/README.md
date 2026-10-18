# ODCrypt

Open source replacement for the prebuilt `ODCrypt.dll` that used to ship in `Required dlls/`. It keeps the assembly name, namespace and the public members this repository calls, so call sites didn't change. It was written from how this repository uses the library and from the public algorithm specifications. Nothing was taken from the original binary.

| Member | What it does |
|---|---|
| `MD5.Hash(byte[])` | RFC 1321 MD5, in managed code so FIPS-enforcing machines get the same bytes. Legacy user password hashes (`MD5`, `MD5_ECW`), signature keys and file checksums depend on it. |
| `Sha3.Hash(byte[])` | FIPS 202 SHA3-512 (Keccak-f[1600]). .NET Framework 4.8 doesn't have SHA-3. `SHA3_512` user password hashes are `base64(SHA3-512(UTF-16LE(salt + password)))`. |
| `CryptUtil.ConstantEquals(string, string)` | Comparison whose running time doesn't depend on where the strings differ |
| `CryptUtil.GenerateSalt(int n)` | base64 of `n` random bytes (`""` for 0) |
| `CryptUtil.RandomString(int bits)` | base64 of `bits / 8` random bytes, rounded up |
| `CryptUtil.Random<T>()` | A random primitive value. Signed integers are non-negative, because callers use them with `%` as an index. |
| `Encryption.CompressString` / `DecompressString` | UTF-8, then gzip, then base64, and back. `DecompressString` returns false for input that isn't compressed, so callers can fall back to the raw text. |
| `Encryption.EncryptString` | Throws `NotSupportedException`. It was used only for an upload to Open Dental's HQ servers. |

The hashes are standard algorithms, so existing password hashes keep verifying byte for byte. `tests/CryptoTests` checks them against published test vectors.
