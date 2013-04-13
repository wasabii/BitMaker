#pragma once

// signature of managed status function
typedef bool (*checkFunc)(int);

// signature of unmanaged search implementation
bool __AvxSearch(unsigned int *round1State, unsigned char *round1Block2, unsigned __int32 *round2State, unsigned char *round2Block1, unsigned __int32 *nonce_, checkFunc check);
