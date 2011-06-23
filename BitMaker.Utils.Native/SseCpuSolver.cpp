#include "stdafx.h"

#include "SseCpuSolver.h"
#include "sha256_sse_x64.cpp"

using namespace System;
using namespace System::Runtime::InteropServices;

#pragma managed(push, off)

typedef bool (*checkFunc)(int);

bool __Solve(unsigned int *round1State, byte *round1Block1, unsigned int *round2State, byte *round2Block1, unsigned int *nonce_, checkFunc check)
{
    unsigned int nonce = *nonce_;

    __sha256_hash_t *round1States[4];
    round1States[0] = (__sha256_hash_t*)round1State;
    round1States[1] = (__sha256_hash_t*)round1State;
    round1States[2] = (__sha256_hash_t*)round1State;
    round1States[3] = (__sha256_hash_t*)round1State;

    __sha256_block_t round1Block2_0;
    __sha256_block_t round1Block2_1;
    __sha256_block_t round1Block2_2;
    __sha256_block_t round1Block2_3;
    
    byte *round1Block2 = round1Block1 + sizeof(__sha256_block_t);
    memcpy(round1Block2, &round1Block2_0, sizeof(__sha256_block_t));
    memcpy(round1Block2, &round1Block2_1, sizeof(__sha256_block_t));
    memcpy(round1Block2, &round1Block2_2, sizeof(__sha256_block_t));
    memcpy(round1Block2, &round1Block2_3, sizeof(__sha256_block_t));

    __sha256_block_t *round1Blocks2[4];
    round1Blocks2[0] = &round1Block2_0;
    round1Blocks2[1] = &round1Block2_1;
    round1Blocks2[2] = &round1Block2_2;
    round1Blocks2[3] = &round1Block2_3;

    __sha256_hash_t *round2States[4];
    round2States[0] = (__sha256_hash_t*)round2State;
    round2States[1] = (__sha256_hash_t*)round2State;
    round2States[2] = (__sha256_hash_t*)round2State;
    round2States[3] = (__sha256_hash_t*)round2State;

    __sha256_block_t round2Block1_0;
    __sha256_block_t round2Block1_1;
    __sha256_block_t round2Block1_2;
    __sha256_block_t round2Block1_3;
    
    memcpy(round2Block1, &round2Block1_0, sizeof(__sha256_block_t));
    memcpy(round2Block1, &round2Block1_1, sizeof(__sha256_block_t));
    memcpy(round2Block1, &round2Block1_2, sizeof(__sha256_block_t));
    memcpy(round2Block1, &round2Block1_3, sizeof(__sha256_block_t));

    __sha256_block_t *round2Blocks1[4];
    round2Blocks1[0] = &round2Block1_0;
    round2Blocks1[1] = &round2Block1_1;
    round2Blocks1[2] = &round2Block1_2;
    round2Blocks1[3] = &round2Block1_3;

    __sha256_hash_t *round2Block1AsHash[4];
    round2Block1AsHash[0] = (__sha256_hash_t*)&round2Block1_0;
    round2Block1AsHash[1] = (__sha256_hash_t*)&round2Block1_1;
    round2Block1AsHash[2] = (__sha256_hash_t*)&round2Block1_2;
    round2Block1AsHash[3] = (__sha256_hash_t*)&round2Block1_3;
    
    __sha256_hash_t round2Hash_0;
    __sha256_hash_t round2Hash_1;
    __sha256_hash_t round2Hash_2;
    __sha256_hash_t round2Hash_3;

    __sha256_hash_t *round2Hashes[4];
    round2Hashes[0] = &round2Hash_0;
    round2Hashes[1] = &round2Hash_1;
    round2Hashes[2] = &round2Hash_2;
    round2Hashes[3] = &round2Hash_3;

    for (;;)
    {
        round1Block2_0[3] = nonce + 0;
        round1Block2_1[3] = nonce + 1;
        round1Block2_2[3] = nonce + 2;
        round1Block2_3[3] = nonce + 3;

        __sha256_int(round1States, round1Blocks2, round2Block1AsHash);
        __sha256_int(round2States, round2Blocks1, round2Hashes);

        for (int i = 0; i < 4; i++)
            if (round2Hashes[7] = 0)
            {
                *nonce_ = nonce + i;
                return true;
            }

        if (nonce % 1048576 == 0)
            if (!check(1048576))
                return false;

        nonce += 4;
    }

    return false;
}

#pragma managed(pop)

namespace BitMaker
{

    namespace Utils
    {

        namespace Native
        {

            public delegate bool CheckDelegate(int hashCount);

            public ref class SseCpuHasher sealed abstract
            {

                public: static Nullable<unsigned int> Solve(unsigned int* round1State, byte* round1Block1, unsigned int* round2State, byte* round2Block1, CheckDelegate^ check) 
                {
                    unsigned int nonce = 0;
                    
                    // create function pointer for 'check', and ensure it doesn't get collected
                    GCHandle gch = GCHandle::Alloc(check);
                    checkFunc checkPtr = (checkFunc)(void*)Marshal::GetFunctionPointerForDelegate(check);

                    try
                    {
                        if (__Solve(round1State, round1Block1, round2State, round2Block1, &nonce, checkPtr))
                            return Nullable<unsigned int>(nonce);
                        else
                            return Nullable<unsigned int>();
                    }
                    finally
                    {
                        gch.Free();
                    }
                }

            };

        }

    }

}