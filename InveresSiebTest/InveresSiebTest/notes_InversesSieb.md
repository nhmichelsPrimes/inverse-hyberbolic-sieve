````markdown
# Testing and Validation Guide
### Inverse Sieve Prototype — Hyperbolic Orbits with Disturbance Filter

---

## 1. Überblick

Das Programm untersucht für Primzahlen `z ≡ 1 (mod 12)` die hyperbolischen Kurven

```text
p² − 12 n² = z
````

und führt dabei:

1. Suche nach Startlösungen `(p, n)` (Seeds),
2. Reduktion auf kanonische Seeds,
3. Orbit-Iteration mit der Einheit ε² = 7 + 4√3,
4. lokales Normsieb:

   ```text
   p ≡ ± 2 t n (mod z),  t² ≡ 3 (mod z),
   ```
5. Störgrößenfilter über lokal zulässige Störprimes `q`,
6. Ausgabe der überlebenden Punkte als globale Fläche `F = {(p, n, z)}`.

Die wichtigsten Konfigurationsparameter sind:

```csharp
const int Z_MAX = 1000;
const int N_SEARCH_MAX = 1000;
const int STEPS_PER_SEED = 10;
const int DISTURBANCE_Q_MAX = 1000;
```

---

## 2. Build und Ausführung

### 2.1 Build

Voraussetzung: .NET SDK (z. B. .NET 7)

```bash
dotnet build
```

### 2.2 Ausführung

```bash
dotnet run
```

Dabei werden zwei CSV-Dateien erzeugt:

* `inverse_sieve_survivors.csv`
* `surface_F.csv`

---

## 3. Struktur der Ausgabedateien

### 3.1 `inverse_sieve_survivors.csv`

Header:

```text
z;seedIndex;k;p;n;isPPrime
```

Spalten:

* `z` — Primzahl mit `z ≡ 1 (mod 12)`
* `seedIndex` — Index des kanonischen Seeds
* `k` — Orbit-Schritt
* `p` — aktueller Wert entlang des Orbits
* `n` — zugehöriger Parameter
* `isPPrime` — Ergebnis des Primtests für `p`

### 3.2 `surface_F.csv`

Header:

```text
p1;n;z
```

Spalten:

* `p1` — der aktuell generierte Wert `p`
* `n` — zugehöriger Parameter
* `z` — zugehörige Hyperbel-Primzahl

---

## 4. Tests der Kernlogik

### 4.1 Test der Hyperbelgleichung

Für jede Survivor-Zeile gilt:

```text
p*p - 12*n*n == z
```

Test:

* Stichproben ziehen oder alles prüfen
* Links berechnen
* Vergleich mit `z`

---

### 4.2 Orbit-Invarianz

Forward-Schritt:

```text
p' = 7p + 24n
n' = 2p + 7n
```

Test:

1. Einen Seed `(p0, n0)` ausgeben lassen
2. Step-für-Step nachrechnen
3. Mit der Programmausgabe (`k = 0 … STEPS_PER_SEED`) vergleichen

Alle müssen exakt übereinstimmen.

---

### 4.3 Kanonische Seed-Reduktion

Rückwärtsschritt:

```text
p_prev = 7p - 24n
n_prev = -2p + 7n
```

Test:

* Prüfen, dass alle zulässigen Rückwärtsschritte die Gleichung erhalten:

  ```
  p_prev*p_prev - 12*n_prev*n_prev == z
  ```
* Abbruchkriterien validieren:

  * `pPrev <= 0`
  * oder Gleichung nicht mehr erfüllt
  * oder `|pPrev| >= |p|`

Seeds müssen eindeutig sein.

---

## 5. Lokales Normsieb

Normbedingung:

```
p ≡ ± 2 t n (mod z)
```

mit

```
t² ≡ 3 (mod z)
```

Test:

1. `FindT(z)` überprüfen:

   * `(t*t) % z == 3 % z`
2. Für jede Survivor-Zeile prüfen:

   ```
   lhs = p mod z
   rhs = (2*t*n) mod z
   rhs_neg = (-rhs) mod z
   lhs == rhs || lhs == rhs_neg
   ```

---

## 6. Störgrößen-Filter

### 6.1 Richtigkeit der erzeugten q-Liste

Für jedes `q` in:

```csharp
GetDisturbancePrimesForZ(z, DISTURBANCE_Q_MAX)
```

muss gelten:

* `q` ist prim (`IsPrime(q)` prüfen)
* `q != 2`, `q != 3`, `q != z`
* `A = -z * 12^{-1} mod q` ist quadratischer Rest modulo q

Programmtest:

1. `inv12 = ModInverse(12, q)`
2. `A = (-z * inv12) mod q`
3. Prüfen:

   ```
   IsQuadraticResidue(A, q) == true
   ```

### 6.2 Anwendung des Filters

Regel:

* Primzahlen gehen **immer** durch
* Composites überleben **nur**, wenn sie **keinen** Störprime-Faktor besitzen

* Anmerkung: das Sieb würde laut Theorie perfekt Funktionieren, wenn man alle Störprimes identifiziert. 
* Das ist der Praxis untauglich: die tatsächlich erlaubten Orbits sind rar. Daher: die Prims werden 
* schnell groß. Praktisch kommt aber jede Primzahl bis Wurzel(p) in Frage... Allerdings sind durch die
* lokalen Bedingungen (2tn schränkt nicht nur p mod z ein - 
* es schränkt vorallem auch die erlaubten Orbits massiv ein) der Suchbereich bereits hinreichend klein. 
* Daher ist es ausreichend, kleine Perioden (Prim-Orbits) auszuschließen. Siehe Beispiel 157 im Paper.

Test:

* Für `isPPrime == false` muss gelten:

  ```
  p % q != 0  für alle q in disturbanceQs
  ```

---

## 7. Validierung des Primtests

Die Routine nutzt Trial Division:

```csharp
IsPrime(long n)
```

Test:

* Survivor-p-Werte gegen externen Primtest prüfen (Python, PARI/GP)
* Sicherstellen:

  * `isPPrime == true` ↔ n ist prim

---

## 8. Fläche F – Globale Struktur

### 8.1 Hyperbelkonsistenz

Alle Zeilen in `surface_F.csv` müssen erfüllen:

```text
p1*p1 - 12*n*n == z
```

### 8.2 Log-Log-Geradenstruktur

* Für jedes `z`:

  * `log(p1)` gegen `log(n)` plotten
  * Punkte müssen auf einer gemeinsamen Geraden liegen
  * Theoretischer Grenzwert: `p/n → 2√3`

* Ein sehr schönes Ergebnis dieser Arbeit ist die Erkenntnis, das `log(p1)` gegen `log(n)` 
* ein lineare Funktion ist. Interessant ist es, den Fehler `e=p1 - 6/3^0,5*n` zu bestimme 
* und log(e) gegen log(n) zu plotten. Daran sieht man dann das Falsifizierungskriterium.

---

## 9. Parameter-Variationen

### 9.1 `DISTURBANCE_Q_MAX`

* Bei 1 → nahezu kein Filter
* Bei 1000 → deutliche Reduktion von Composites

Erwartung:

* Survivor-Menge ändert sich, aber **Struktur bleibt stabil**

### 9.2 `N_SEARCH_MAX`

* Erhöhen → mehr Seeds
* Gleichbleibende Seeds müssen gleich bleiben
* Orbitstruktur darf sich nicht ändern

---

## 10. Zusammenfassung der Validierung

Das Programm erfüllt seine Funktion, wenn:

* Hyperbelgleichung **immer** korrekt bleibt
* Orbits **exakt** reproduzierbar sind
* Seeds eindeutig sind
* Lokales Normsieb **immer** erfüllt ist
* Störfilter korrekt wirkt
* Oberfläche `F` vollständig erzeugt wird
* Log-Log-Geraden sichtbar sind




------------------------------------------------------------

IT License

Copyright (c) 2025 Norman-Hendrik Michels

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.