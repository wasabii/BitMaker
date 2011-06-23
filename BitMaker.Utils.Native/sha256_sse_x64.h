#pragma once

typedef uint32_t __sha256_hash_t[8];
typedef uint8_t  __sha256_block_t[64];

void __sha256_int(__sha256_hash_t *states[4], __sha256_block_t *blocks[4], __sha256_hash_t *outputs[4]);
