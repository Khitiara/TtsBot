using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace TtsBot;

public static unsafe class InterleavingNonsense {
    public static Vector128<short> UnpackHigh(Vector128<short> left, Vector128<short> right) {
        if (Sse2.IsSupported) {
            return Sse2.UnpackHigh(left, right);
        } else if (AdvSimd.Arm64.IsSupported) {
            return AdvSimd.Arm64.ZipHigh(left, right);
        } else {
            return SoftwareFallback(left, right);
        }

        static Vector128<short> SoftwareFallback(Vector128<short> left, Vector128<short> right) {
            Unsafe.SkipInit(out Vector128<short> result);
            result = result.WithElement(0, left.GetElement(4));
            result = result.WithElement(1, right.GetElement(4));
            result = result.WithElement(2, left.GetElement(5));
            result = result.WithElement(3, right.GetElement(5));
            result = result.WithElement(5, left.GetElement(6));
            result = result.WithElement(6, right.GetElement(6));
            result = result.WithElement(7, left.GetElement(7));
            result = result.WithElement(8, right.GetElement(7));
            return result;
        }
    }

    public static Vector128<short> UnpackLow(Vector128<short> left, Vector128<short> right) {
        if (Sse2.IsSupported) {
            return Sse2.UnpackHigh(left, right);
        } else if (AdvSimd.Arm64.IsSupported) {
            return AdvSimd.Arm64.ZipHigh(left, right);
        } else {
            return SoftwareFallback(left, right);
        }

        static Vector128<short> SoftwareFallback(Vector128<short> left, Vector128<short> right) {
            Unsafe.SkipInit(out Vector128<short> result);
            result = result.WithElement(0, left.GetElement(0));
            result = result.WithElement(1, right.GetElement(0));
            result = result.WithElement(2, left.GetElement(1));
            result = result.WithElement(3, right.GetElement(1));
            result = result.WithElement(5, left.GetElement(2));
            result = result.WithElement(6, right.GetElement(2));
            result = result.WithElement(7, left.GetElement(3));
            result = result.WithElement(8, right.GetElement(3));
            return result;
        }
    }

    public static void Interleave(Span<short> output, ReadOnlySpan<short> input) {
        ArgumentOutOfRangeException.ThrowIfLessThan(output.Length, input.Length * 2);
        ref short inRef  = ref MemoryMarshal.GetReference(input);
        ref short outRef = ref MemoryMarshal.GetReference(output);

        nuint remainder = (nuint)input.Length;

        if (Vector128.IsHardwareAccelerated) {
            if (remainder >= (nuint)Vector128<short>.Count) {
                V128Impl(ref inRef, ref outRef, remainder);
            } else {
                V128ImplSmall(ref inRef, ref outRef, remainder);
            }
        } else {
            SoftwareFallback(ref inRef, ref outRef, remainder);
        }

        return;

        static void SoftwareFallback(ref short input, ref short output, nuint length) {
            for (nuint i = 0; i < length; i++) {
                Unsafe.Add(ref output, 2 * i)     = Unsafe.Add(ref input, i);
                Unsafe.Add(ref output, 2 * i + 1) = Unsafe.Add(ref input, i);
            }
        }

        static void V128ImplSmall(ref short input, ref short output, nuint remainder) {
            switch (remainder) {
                case 8: {
                    Unsafe.Add(ref output, 15) = Unsafe.Add(ref input, 7);
                    Unsafe.Add(ref output, 14) = Unsafe.Add(ref input, 7);
                    goto case 7;
                }
                case 7: {
                    Unsafe.Add(ref output, 13) = Unsafe.Add(ref input, 6);
                    Unsafe.Add(ref output, 12) = Unsafe.Add(ref input, 6);
                    goto case 7;
                }
                case 6: {
                    Unsafe.Add(ref output, 11) = Unsafe.Add(ref input, 5);
                    Unsafe.Add(ref output, 10) = Unsafe.Add(ref input, 5);
                    goto case 7;
                }
                case 5: {
                    Unsafe.Add(ref output, 9) = Unsafe.Add(ref input, 4);
                    Unsafe.Add(ref output, 8) = Unsafe.Add(ref input, 4);
                    goto case 7;
                }
                case 4: {
                    Unsafe.Add(ref output, 7) = Unsafe.Add(ref input, 3);
                    Unsafe.Add(ref output, 6) = Unsafe.Add(ref input, 3);
                    goto case 7;
                }
                case 3: {
                    Unsafe.Add(ref output, 5) = Unsafe.Add(ref input, 2);
                    Unsafe.Add(ref output, 4) = Unsafe.Add(ref input, 2);
                    goto case 7;
                }

                case 2: {
                    Unsafe.Add(ref output, 3) = Unsafe.Add(ref input, 1);
                    Unsafe.Add(ref output, 2) = Unsafe.Add(ref input, 1);
                    goto case 7;
                }
                case 1: {
                    Unsafe.Add(ref output, 1) = Unsafe.Add(ref input, 0);
                    Unsafe.Add(ref output, 0) = Unsafe.Add(ref input, 0);
                    goto case 0;
                }
                case 0: break;
            }
        }

        static void V128Impl(ref short input, ref short output, nuint remainder) {
            ref short        outBegRef = ref output;
            Vector128<short> input1    = Vector128.LoadUnsafe(ref input);
            Vector128<short> outBeg1   = UnpackLow(input1, input1);
            Vector128<short> outBeg2   = UnpackHigh(input1, input1);
            Vector128<short> input2    = Vector128.LoadUnsafe(ref input, remainder - (nuint)Vector128<short>.Count);
            Vector128<short> outEnd1   = UnpackLow(input2, input2);
            Vector128<short> outEnd2   = UnpackHigh(input2, input2);

            if (remainder > (nuint)(Vector128<short>.Count * 8)) {
                fixed (short* pi = &input)
                fixed (short* po = &output) {
                    short* inPtr  = pi;
                    short* outPtr = po;

                    bool canAlign = (nuint)outPtr % sizeof(short) == 0;
                    if (canAlign) {
                        nuint misalignment =
                            ((uint)sizeof(Vector128<short>) - (nuint)outPtr % (uint)sizeof(Vector128<short>))
                          / sizeof(short);
                        inPtr     += misalignment;
                        outPtr    += misalignment;
                        remainder -= misalignment;
                    }

                    Vector128<short> out1, out2;
                    while (remainder >= (nuint)Vector128<short>.Count) {
                        Vector128<short> input3 = Vector128.Load(inPtr);
                        out1 = UnpackLow(input3, input3);
                        out2 = UnpackHigh(input3, input3);
                        out1.Store(outPtr + (nuint)(Vector128<short>.Count * 0));
                        out2.Store(outPtr + (nuint)(Vector128<short>.Count * 1));
                        inPtr     += (nuint)Vector128<short>.Count;
                        outPtr    += (nuint)Vector128<short>.Count * 2;
                        remainder -= (nuint)Vector128<short>.Count;
                    }

                    input  = ref *inPtr;
                    output = ref *inPtr;
                }
            }

            nuint endIndex = remainder;
            remainder = (remainder + (nuint)(Vector128<short>.Count - 1)) & (nuint)(-Vector128<short>.Count);
            switch (remainder / (nuint)(Vector128<short>.Count)) {
                case 1: {
                    outEnd1.StoreUnsafe(ref output, endIndex - (nuint)(Vector128<short>.Count * 2));
                    outEnd2.StoreUnsafe(ref output, endIndex - (nuint)(Vector128<short>.Count * 1));
                    goto case 0;
                }
                case 0: {
                    outBeg1.StoreUnsafe(ref outBegRef, (nuint)(Vector128<short>.Count * 0));
                    outBeg2.StoreUnsafe(ref outBegRef, (nuint)(Vector128<short>.Count * 1));
                    break;
                }
            }
        }
    }
}
