#pragma once

// signature of managed status function
typedef bool (*sseCheckFunc)(int);

// signature of unmanaged search implementation
bool __SseSearch(unsigned int *round1State, unsigned char *round1Block2, unsigned __int32 *round2State, unsigned char *round2Block1, unsigned __int32 *nonce_, sseCheckFunc check);
