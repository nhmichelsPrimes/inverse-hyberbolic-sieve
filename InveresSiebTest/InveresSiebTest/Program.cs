using System;
using System.Collections.Generic;
using System.IO;

namespace InversesSiebTest
{
    class Program
    {
        // -------------------------------------------------------
        // Konfiguration
        // -------------------------------------------------------
        const int Z_MAX = 1000;              // maximale z-Primzahl
        const int N_SEARCH_MAX = 1000;     // Suche nach Startlösungen |n| <= N_SEARCH_MAX
        const int STEPS_PER_SEED = 10;       // Anzahl Orbit-Schritte pro Startlösung (höhere Werte machen kaum Sinn, da die generierten Primzahlen schnell ins astronomische wachsen.
        const int DISTURBANCE_Q_MAX = 1000;  // maximale Prüfschranke für Störprimes q

        // Bei großen Primzahlen (die hier sehr schnell erreicht werden) müsste man Disturbance_Q_Max höher setzen - es bestimmt die Störorbits, die berechnet werden. 
        // Desto höher, desto besser der Filter. Kann man als Test auch mal auf 1 setzen (praktisch kein Sieb - bzw. nur noch das MOD-Sieb und das Index-Sieb, das bestimmt, dass Primzahlen 
        // nur in einer sehr dünnen Schicht an Orbits überhaupt existieren können.
        // Wenn jemand eine Primzahl außerhalb eines erlaubten Orbits finden sollte, falzifiziert das die Theorie. Viel Erfolg dabei!

        const string CSV_SURVIVORS = "inverse_sieve_survivors.csv";
        const string CSV_SURFACE_F = "surface_F.csv";

        // -------------------------------------------------------
        // Globale Struktur der Fläche F: (p1, n, z)
        // -------------------------------------------------------
        public struct SurfacePoint
        {
            public long P1;   // seed prime p1: die beiden Seed-Primes werden zu Beginn bestimmt (
            public long N;    // Parameter n
            public int Z;     // Primzahl z

            public SurfacePoint(long p1, long n, int z)
            {
                P1 = p1;
                N = n;
                Z = z;
            }
        }

        static readonly List<SurfacePoint> SurfaceF = new List<SurfacePoint>();

        // -------------------------------------------------------
        // Main
        // -------------------------------------------------------
        static void Main(string[] args)
        {
            Console.WriteLine("Starte inversen Siebtest (kanonische Seeds, nur Survivors)...");
            Console.WriteLine($"Z_MAX = {Z_MAX}, N_SEARCH_MAX = {N_SEARCH_MAX}, STEPS_PER_SEED = {STEPS_PER_SEED}");
            Console.WriteLine($"CSV-Ausgabe Survivors: {CSV_SURVIVORS}");
            Console.WriteLine($"CSV-Ausgabe Fläche F: {CSV_SURFACE_F}");

            var primesUpToZ = SievePrimes(Z_MAX);

            using (var writer = new StreamWriter(CSV_SURVIVORS))
            {
                // CSV-Header für Survivors
                writer.WriteLine("z;seedIndex;k;p;n;isPPrime");

                foreach (var z in primesUpToZ)
                {
                    // Wir betrachten nur z = 1 (mod 12)
                    if (z % 12 != 1) continue;

                    Console.WriteLine();
                    Console.WriteLine($"Bearbeite z = {z}...");

                    int t = FindT(z);
                    if (t == -1)
                    {
                        Console.WriteLine($"  Kein t mit t^2 ≡ 3 (mod {z}) gefunden, überspringe.");
                        continue;
                    }

                    // Störgrößen nach Theorie bestimmen
                    var disturbanceQs = GetDisturbancePrimesForZ(z, DISTURBANCE_Q_MAX);
                    Console.WriteLine($"  Störgrößen q für z={z}: {string.Join(",", disturbanceQs)}");

                    var seeds = FindCanonicalSeeds(z, N_SEARCH_MAX);
                    if (seeds.Count == 0)
                    {
                        Console.WriteLine($"  Keine Seeds für z = {z} in |n| <= {N_SEARCH_MAX} gefunden.");
                        continue;
                    }

                    Console.WriteLine($"  {seeds.Count} kanonische Seeds gefunden.");
                    int seedIndex = 0;

                    foreach (var seed in seeds)
                    {
                        long p = seed.p;
                        long n = seed.n;

                        Console.WriteLine($"  Seed {seedIndex}: p0 = {p}, n0 = {n}");

                        for (int k = 0; k < STEPS_PER_SEED; k++)
                        {
                            bool eqOk = (p * p - 12L * n * n == z);
                            bool localOk = CheckLocalSieve(p, n, z, t);
                            bool isPPrime = IsPrime(p);

                            // Störgrößenfilter nur für Composites
                            bool divisibleByDisturbance = false;
                            if (!isPPrime)
                            {
                                foreach (var q in disturbanceQs)
                                {
                                    if (p % q == 0)
                                    {
                                        divisibleByDisturbance = true;
                                        break;
                                    }
                                }
                            }

                            // Survivors:
                            // - Gleichung stimmt
                            // - lokales Normsieb stimmt
                            // - Primzahlen behalten wir immer
                            // - zusammengesetzte p nur, wenn sie keinen Störprime-Faktor haben
                            bool survives =
                                eqOk &&
                                localOk &&
                                !divisibleByDisturbance;

                            if (survives)
                            {
                                // CSV-Ausgabe für Survivors
                                writer.WriteLine(
                                    $"{z};{seedIndex};{k};{p};{n};{isPPrime}");

                                // Punkt auf der Fläche F:
                                // p1 = p, n = n, z = z
                                // (entspricht einem Punkt der globalen Fläche)
                                SurfaceF.Add(new SurfacePoint(p, n, z));
                            }

                            // Nächster Orbit-Schritt (ε²-Schritt)
                            NextOrbitStep(ref p, ref n);
                        }

                        seedIndex++;
                    }
                }
            }

            // Nach der Hauptschleife: gesamte Fläche F ausgeben
            WriteSurfaceF();

            Console.WriteLine();
            Console.WriteLine("Fertig. Survivors-CSV und surface_F.csv geschrieben.");
        }

        // ------------------------------------------------------
        // Seeds finden und kanonisch machen
        // -------------------------------------------------------

        /// <summary>
        /// Findet alle (p, n) mit p^2 - 12 n^2 = z in 1 <= n <= maxN
        /// und reduziert sie per Rückwärtsschritt auf kanonische Seeds.
        /// </summary>
        static List<(long p, long n)> FindCanonicalSeeds(int z, int maxN)
        {
            var raw = new List<(long p, long n)>();

            // Umkehrabbildung: z = p^2 - 12 n^2 -> p^2 = z + 12 n^2
            // Wir suchen ganzzahlige Quadrate für p^2.
            for (int n = 1; n <= maxN; n++)
            {
                long val = z + 12L * n * n;
                long p = (long)Math.Sqrt(val);
                if (p * p == val)
                {
                    raw.Add((p, n));
                }
            }

            // Reduktion auf kanonische Seeds durch Rückwärtsschritte
            var seeds = new List<(long p, long n)>();
            var seen = new HashSet<string>();

            foreach (var (p0, n0) in raw)
            {
                var reduced = ReduceSeed(p0, n0, z);
                string key = $"{reduced.p}|{reduced.n}";
                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    seeds.Add(reduced);
                }
            }

            return seeds;
        }

        /// <summary>
        /// Rückwärtsschritt im Orbit:
        /// Inverse zu
        ///   p' = 7p + 24n
        ///   n' = 2p + 7n
        /// ergibt:
        ///   p_prev = 7p - 24n
        ///   n_prev = -2p + 7n
        ///
        /// Wir reduzieren solange, wie:
        /// - wir auf der gleichen Kurve bleiben
        /// - p_prev > 0
        /// - |p_prev| < |p| (wir wollen das "innerste" p)
        /// </summary>
        static (long p, long n) ReduceSeed(long p, long n, int z)
        {
            while (true)
            {
                long pPrev = 7 * p - 24 * n;
                long nPrev = -2 * p + 7 * n;

                if (pPrev <= 0) break;
                if (pPrev * pPrev - 12L * nPrev * nPrev != z) break;
                if (Math.Abs(pPrev) >= Math.Abs(p)) break;

                p = pPrev;
                n = nPrev;
            }
            return (p, n);
        }

        // -------------------------------------------------------
        // Sieblogik / Orbit
        // -------------------------------------------------------

        /// <summary>
        /// Vorwärtsschritt mit ε² = 7 + 4√3:
        /// (p + 2n√3) -> (p' + 2n'√3)
        ///   p' = 7p + 24n
        ///   n' = 2p + 7n
        /// bewahrt die Quadrik z = p^2 - 12n^2.
        /// </summary>
        static void NextOrbitStep(ref long p, ref long n)
        {
            long newP = 7 * p + 24 * n;
            long newN = 2 * p + 7 * n;
            p = newP;
            n = newN;
        }

        /// <summary>
        /// Lokales Normsieb: p ≡ ± 2 t n (mod z)
        /// (t ist eine Lösung von t^2 ≡ 3 (mod z)).
        /// </summary>
        static bool CheckLocalSieve(long p, long n, int z, int t)
        {
            long modZ = z;
            long pMod = Mod(p, modZ);
            long twoT = Mod(2L * t, modZ);
            long nMod = Mod(n, modZ);

            long rhs = Mod(twoT * nMod, modZ);
            long rhsNeg = Mod(-rhs, modZ);

            return pMod == rhs || pMod == rhsNeg;
        }

        /// <summary>
        /// Findet t mit t^2 ≡ 3 (mod z), z prim.
        /// Falls keine Lösung existiert, wird -1 zurückgegeben.
        /// </summary>
        static int FindT(int z)
        {
            int target = ((3 % z) + z) % z;
            for (int t = 1; t < z; t++)
            {
                if ((long)t * t % z == target) return t;
            }
            return -1;
        }

        // -------------------------------------------------------
        // Störgrößen nach Theorie
        // -------------------------------------------------------

        /// <summary>
        /// Liefert alle Primes q <= qMax (q != 2,3,z),
        /// für die -z * 12^{-1} ein quadratischer Rest mod q ist.
        /// Das sind genau die lokal zulässigen Störprimes für dieses z.
        /// </summary>
        static List<int> GetDisturbancePrimesForZ(int z, int qMax)
        {
            var qs = new List<int>();
            var primes = SievePrimes(qMax);

            foreach (var q in primes)
            {
                if (q == 2 || q == 3 || q == z) continue;

                long inv12 = ModInverse(12, q);  // q ist prim, 12 nicht durch q teilbar
                long A = (-1L * z * inv12) % q;
                if (A < 0) A += q;

                if (IsQuadraticResidue(A, q))
                    qs.Add(q);
            }

            return qs;
        }

        /// <summary>
        /// Prüft, ob a ein quadratischer Rest modulo q ist (q prim).
        /// Euler-Kriterium: a^((q-1)/2) ≡ 1 (mod q).
        /// </summary>
        static bool IsQuadraticResidue(long a, int q)
        {
            a %= q;
            if (a < 0) a += q;
            if (a == 0) return true;

            long e = (q - 1) / 2;
            long r = ModPow(a, e, q);
            return r == 1;
        }

        /// <summary>
        /// Modular exponentiation: b^e mod m.
        /// </summary>
        static long ModPow(long b, long e, int m)
        {
            long res = 1 % m;
            b %= m;
            if (b < 0) b += m;
            while (e > 0)
            {
                if ((e & 1) != 0)
                    res = (res * b) % m;
                b = (b * b) % m;
                e >>= 1;
            }
            return res;
        }

        /// <summary>
        /// Modular inverse von a modulo m (m prim), via Fermat: a^(m-2) mod m.
        /// </summary>
        static long ModInverse(long a, int m)
        {
            return ModPow(a, m - 2, m);
        }

        // -------------------------------------------------------
        // Primzahlen & Helper
        // -------------------------------------------------------

        static bool IsPrime(long n)
        {
            if (n < 2) return false;
            if (n == 2 || n == 3) return true;
            if (n % 2 == 0) return false;
            if (n < 9) return true;

            long limit = (long)Math.Sqrt(n);
            for (long d = 3; d <= limit; d += 2)
            {
                if (n % d == 0) return false;
            }
            return true;
        }

        /// <summary>
        /// Standard-Sieb (Eratosthenes) bis max.
        /// </summary>
        static List<int> SievePrimes(int max)
        {
            var isComposite = new bool[max + 1];
            var primes = new List<int>();
            for (int i = 2; i <= max; i++)
            {
                if (!isComposite[i])
                {
                    primes.Add(i);
                    if ((long)i * i <= max)
                    {
                        for (int j = i * i; j <= max; j += i)
                            isComposite[j] = true;
                    }
                }
            }
            return primes;
        }

        static long Mod(long x, long m)
        {
            long r = x % m;
            if (r < 0) r += m;
            return r;
        }

        // -------------------------------------------------------
        // Ausgabe der Fläche F
        // -------------------------------------------------------

        static void WriteSurfaceF()
        {
            using (var fw = new StreamWriter(CSV_SURFACE_F))
            {
                fw.WriteLine("p1;n;z");
                foreach (var sp in SurfaceF)
                {
                    fw.WriteLine($"{sp.P1};{sp.N};{sp.Z}");
                }
            }
        }
    }
}

