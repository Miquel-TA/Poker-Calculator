# Calculadora de Poker Hold'em

Una API mínima en **.NET 8** que calcula las probabilidades de victoria en Texas Hold'em mediante simulaciones de Monte Carlo.

---

## Características

* **Simulación Monte Carlo**: Ejecuta 200,000 iteraciones para estimar la equidad de la mano.
* **Interfaz Web**: Incluye una página sencilla en HTML y CSS para realizar consultas.
* **Parámetros Ajustables**: Permite configurar cartas de mano, mesa (Flop, Turn, River) y entre 1 y 9 oponentes.

---

## Formato de Entrada

El sistema utiliza una nomenclatura específica para las cartas:

| Palo | Código | Ejemplo |
| --- | --- | --- |
| **Picas** | `P` | `AP` (As de Picas) |
| **Corazones** | `C` | `KC` (Rey de Corazones) |
| **Tréboles** | `T` | `10T` (Diez de Tréboles) |
| **Diamantes** | `D` | `7D` (Siete de Diamantes) |

---

## API Endpoints

### `GET /`

Carga la interfaz de usuario en el navegador.

### `POST /calculate`

Calcula la probabilidad basada en el estado actual de la partida.

**Cuerpo de la petición (JSON):**

```json
{
  "holeCards": "AP KP",
  "boardCards": "QP JP 2C",
  "opponents": 3
}

```

**Respuesta (JSON):**

```json
{
  "winProbabilityPercentage": 45.32,
  "executionTimeSeconds": 0.1245,
  "iterations": 200000,
  "opponents": 3
}

```

---
