#include "stdafx.h"

#include "AvxMinerUtils.h"
#include "AvxMinerUtils_cpp.h"

using namespace System;
using namespace System::Runtime::InteropServices;

namespace BitMaker
{

    namespace Utils
    {

        namespace Native
        {

            public delegate bool AvxCheckDelegate(unsigned int hashCount);

            public ref class AvxMinerUtils sealed abstract
            {

            public:

                static Nullable<unsigned int> Search(unsigned int* round1State, unsigned char* round1Block1, unsigned int* round2State, unsigned char* round2Block1, AvxCheckDelegate^ check)
                {
                    unsigned int nonce;
                    
                    // create function pointer for 'status', and ensure it doesn't get collected
                    GCHandle checkH = GCHandle::Alloc(check);
                    checkFunc checkPtr = (checkFunc)(void*)Marshal::GetFunctionPointerForDelegate(check);

                    try
                    {
                        if (__AvxSearch(round1State, round1Block1, round2State, round2Block1, &nonce, checkPtr))
                            return Nullable<unsigned int>(nonce);
                        else
                            return Nullable<unsigned int>();
                    }
                    finally
                    {
                        checkH.Free();
                    }
                }

            };

        }

    }

}