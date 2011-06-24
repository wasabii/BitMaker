#include "stdafx.h"

#include "SseCpuSolver.h"
#include "sha256_sse_x64.cpp"

using namespace System;
using namespace System::Runtime::InteropServices;

#pragma managed(push, off)

typedef bool (*checkFunc)(int);

bool __Solve(unsigned int *round1State, unsigned char *round1Block2, unsigned __int32 *round2State, unsigned char *round2Block1, unsigned __int32 *nonce_, checkFunc check)
{
    unsigned int nonce = *nonce_;

    // vector containing input round1 state
    __m128i round1State_m128i[8];
    for (int i = 0; i < 8; i++)
        round1State_m128i[i] = _mm_set1_epi32(round1State[i]);

    // vector containing input round 1 block 2, contains the nonce field
    __m128i round1Block2_m128i[16];
    for (int i = 0; i < 16; i++)
        round1Block2_m128i[i] = _mm_set1_epi32(((unsigned int*)round1Block2)[i]);

    // vector containing input round 2 state, initialized
    __m128i round2State_m128i[8];
    for (int i = 0; i < 8; i++)
        round2State_m128i[i] = _mm_set1_epi32(round2State[i]);

    // vector containing round 2 block, to which the state from round 1 should be output
    __m128i round2Block1_m128i[16];
    for (int i = 0; i < 16; i++)
        round2Block1_m128i[i] = _mm_set1_epi32(((unsigned int*)round2Block1)[i]);

    // vector containing the final output from round 2
    __m128i round2State2_m128i[8];

    // vector containing integers to incremenet nonce values per-hash
    __m128i nonce_inc_m128i = _mm_set1_epi32(4);

    // initial nonce vector
    __m128i nonce_m128i = _mm_set_epi32(nonce + 0, nonce + 1, nonce + 2, nonce + 3);

    unsigned int count = 0;

    for (;;)
    {
        // store the new nonce values into round 1 block 2
        round1Block2_m128i[3] = nonce_m128i;

        __sha256_transform(round1State_m128i, round1Block2_m128i, round2Block1_m128i);
        __sha256_transform(round2Block1_m128i, round2State_m128i, round2State2_m128i);
        
        // compare last int of output to zeros, matching words set to 0xFFFFFFFF
        __m128i p = _mm_cmpeq_epi32(round2State2_m128i[7], _mm_setzero_si128());

        // cast into two 64 bit ints, these values let us narrow down whether any bits are set
        unsigned __int64 *p64 = (unsigned __int64*)&p;

        count++;

        // one of the two sides of the vector has values
        if ((p64[0] != 0) | (p64[1] != 0))
        {
            // first result
            if (_mm_extract_epi16(p, 0) != 0)
            {
                *nonce_ = nonce += 0;
                return true;
            }

            // second result
            if (_mm_extract_epi16(p, 2) != 0)
            {
                *nonce_ = nonce += 1;
                return true;
            }
            
            // third result
            if (_mm_extract_epi16(p, 4) != 0)
            {
                *nonce_ = nonce += 2;
                return true;
            }

            // fourth result
            if (_mm_extract_epi16(p, 6) != 0)
            {
                *nonce_ = nonce += 3;
                return true;
            }
        }

        if ((nonce % 131072) == 0 && nonce > 0)
        {
            if (!check(count))
                return false;
            count = 0;
        }

        nonce_m128i = _mm_add_epi32(nonce_m128i, nonce_inc_m128i);
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

                public: static Nullable<unsigned int> Solve(unsigned int* round1State, unsigned char* round1Block1, unsigned int* round2State, unsigned char* round2Block1, CheckDelegate^ check) 
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