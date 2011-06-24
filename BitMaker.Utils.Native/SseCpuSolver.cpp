#include "stdafx.h"

#include "SseCpuSolver.h"
#include "sha256_sse_x64.cpp"

using namespace System;
using namespace System::Runtime::InteropServices;

#pragma managed(push, off)

#define endian_swap(value) ((value & 0x000000ffU) << 24 | (value & 0x0000ff00U) << 8 | (value & 0x00ff0000U) >> 8 | (value & 0xff000000U) >> 24)

#define mm_or4(a, b, c, d) (_mm_or_si128(_mm_or_si128(_mm_or_si128(a, b), c), d))

#define mm_endian_swap(value) ( \
        mm_or4( \
            _mm_slli_epi32(_mm_and_si128(value, _mm_set1_epi32(0x000000ff)), 24), \
            _mm_slli_epi32(_mm_and_si128(value, _mm_set1_epi32(0x0000ff00)), 8), \
            _mm_srli_epi32(_mm_and_si128(value, _mm_set1_epi32(0x00ff0000)), 8), \
            _mm_srli_epi32(_mm_and_si128(value, _mm_set1_epi32(0xff000000)), 24)))

typedef bool (*statusFunc)(int);

bool __Solve(unsigned int *round1State, unsigned char *round1Block2, unsigned __int32 *round2State, unsigned char *round2Block1, unsigned __int32 *nonce_, statusFunc status)
{
    // start at existing nonce
    unsigned int nonce = endian_swap(((unsigned int*)round1Block2)[3]);

    // vector containing input round1 state
    __m128i round1State_m128i[8];
    for (int i = 0; i < 8; i++)
        round1State_m128i[i] = _mm_set1_epi32(round1State[i]);

    // vector containing input round 1 block 2, contains the nonce field
    __m128i round1Block2_m128i[16];
    for (int i = 0; i < 16; i++)
        round1Block2_m128i[i] = _mm_set1_epi32(((unsigned __int32*)round1Block2)[i]);

    // vector containing input round 2 state, initialized
    __m128i round2State_m128i[8];
    for (int i = 0; i < 8; i++)
        round2State_m128i[i] = _mm_set1_epi32(round2State[i]);

    // vector containing round 2 block, to which the state from round 1 should be output
    __m128i round2Block1_m128i[16];
    for (int i = 0; i < 16; i++)
        round2Block1_m128i[i] = _mm_set1_epi32(((unsigned __int32*)round2Block1)[i]);

    // vector containing the final output from round 2
    __m128i round2State2_m128i[8];

    // initial nonce vector
    __m128i nonce_inc_m128i = _mm_set_epi32(0, 1, 2, 3);

    unsigned int count = 0;

    for (;;)
    {
        // set nonce in blocks
        round1Block2_m128i[3] = mm_endian_swap(_mm_add_epi32(_mm_set1_epi32(nonce), nonce_inc_m128i));
        
        // transform variable second half of block using saved state from first block, into pre-padded round 2 block (end of first hash)
        sha256_transform(round1State_m128i, round1Block2_m128i, round2Block1_m128i);

        // transform round 2 block into round 2 state (second hash)
        sha256_transform(round2State_m128i, round2Block1_m128i, round2State2_m128i);
        
        // isolate 0x00000000, segment to in64 for easier testing
        __m128i p = _mm_cmpeq_epi32(round2State2_m128i[7], _mm_setzero_si128());
        unsigned __int64 *p64 = (unsigned __int64*)&p;

        // one of the two sides of the vector has values
        if ((p64[0] != 0) | (p64[1] != 0))
        {
            // first result
            if (_mm_extract_epi16(p, 0) != 0)
            {
                *nonce_ = nonce + 3;
                return true;
            }

            // second result
            if (_mm_extract_epi16(p, 2) != 0)
            {
                *nonce_ = nonce + 2;
                return true;
            }
            
            // third result
            if (_mm_extract_epi16(p, 4) != 0)
            {
                *nonce_ = nonce + 1;
                return true;
            }

            // fourth result
            if (_mm_extract_epi16(p, 6) != 0)
            {
                *nonce_ = nonce + 0;
                return true;
            }
        }

        if ((++count % 65536) == 0)
            if (!status(65536))
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

            public delegate bool StatusDelegate(unsigned int hashCount);

            public ref class SseCpuHasher sealed abstract
            {

                public: static Nullable<unsigned int> Solve(unsigned int* round1State, unsigned char* round1Block1, unsigned int* round2State, unsigned char* round2Block1, StatusDelegate^ status)
                {
                    unsigned int nonce;
                    
                    // create function pointer for 'status', and ensure it doesn't get collected
                    GCHandle statchH = GCHandle::Alloc(status);
                    statusFunc statusPtr = (statusFunc)(void*)Marshal::GetFunctionPointerForDelegate(status);

                    try
                    {
                        if (__Solve(round1State, round1Block1, round2State, round2Block1, &nonce, statusPtr))
                            return Nullable<unsigned int>(nonce);
                        else
                            return Nullable<unsigned int>();
                    }
                    finally
                    {
                        statchH.Free();
                    }
                }

            };

        }

    }

}