#pragma once

// signature of managed status function
typedef bool (*statusFunc)(int);

// signature of unmanaged solve implementation
bool __Solve(unsigned int *round1State, unsigned char *round1Block2, unsigned __int32 *round2State, unsigned char *round2Block1, unsigned __int32 *nonce_, statusFunc status);
