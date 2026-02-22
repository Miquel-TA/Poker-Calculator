using HoldemPoker.Cards;
using HoldemPoker.Evaluator;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
var app = builder.Build();

// Static helper for card parsing
List<string> ParseCards(string input) {
    if (string.IsNullOrWhiteSpace(input)) return new List<string>();
    
    var mapSuit = new Dictionary<char, char> { {'C', 'h'}, {'P', 's'}, {'T', 'c'}, {'D', 'd'} };
    var mapRank = new Dictionary<string, string> { 
        {"A", "A"}, {"2", "2"}, {"3", "3"}, {"4", "4"}, {"5", "5"}, 
        {"6", "6"}, {"7", "7"}, {"8", "8"}, {"9", "9"}, {"10", "T"}, 
        {"J", "J"}, {"Q", "Q"}, {"K", "K"} 
    };
    
    var res = new List<string>();
    var parts = input.Trim().ToUpper().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
    
    foreach(var p in parts) {
        if (p.Length < 2) throw new Exception($"Carta no válida: {p}");
        char s = p[^1];
        string r = p[..^1];
        if(!mapSuit.ContainsKey(s) || !mapRank.ContainsKey(r)) throw new Exception($"Formato inválido: {p}");
        res.Add($"{mapRank[r]}{mapSuit[s]}");
    }
    return res;
}

// Frontend UI
app.MapGet("/", () => Results.Content(@"
<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='UTF-8'>
    <title>Calculadora Poker</title>
    <style>
        body { font-family: system-ui, sans-serif; max-width: 600px; margin: 2rem auto; padding: 0 1rem; line-height: 1.5; background: #f4f4f9; }
        .card { background: white; padding: 2rem; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        .field { margin-bottom: 1rem; }
        label { display: block; font-weight: bold; margin-bottom: 0.5rem; }
        input { width: 100%; padding: 0.75rem; border: 1px solid #ccc; border-radius: 4px; box-sizing: border-box; font-size: 1rem; }
        button { background: #007bff; color: white; border: none; padding: 0.75rem 1.5rem; border-radius: 4px; cursor: pointer; width: 100%; font-size: 1rem; font-weight: bold; }
        button:hover { background: #0056b3; }
        #result { margin-top: 1.5rem; padding: 1rem; border-radius: 4px; display: none; }
        .success { background: #d4edda; color: #155724; border: 1px solid #c3e6cb; }
        .error { background: #f8d7da; color: #721c24; border: 1px solid #f5c6cb; }
        small { color: #666; }
    </style>
</head>
<body>
    <div class='card'>
        <h2>Calculadora Poker</h2>
		<h3>Formato: 5P JC equivale a 5 de Picas y J de Corazones</h3>
        <div class='field'>
            <label>Mano</label>
            <input type='text' id='holeCards' placeholder='Cartas en tu mano.' value=''>
        </div>
        <div class='field'>
            <label>Mesa (opcional)</label>
            <input type='text' id='boardCards' placeholder='Cartas en la mesa.'>
        </div>
        <div class='field'>
            <label>Oponentes (1-9)</label>
            <input type='number' id='opponents' value='1' min='1' max='9'>
        </div>
        <button onclick='calculate()'>Calcular</button>
        <div id='result'></div>
    </div>

    <script>
        async function calculate() {
            const btn = document.querySelector('button');
            const resDiv = document.getElementById('result');
            btn.disabled = true;
            btn.innerText = 'Calculando...';
            
            try {
                const response = await fetch('/calculate', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        holeCards: document.getElementById('holeCards').value,
                        boardCards: document.getElementById('boardCards').value,
                        opponents: parseInt(document.getElementById('opponents').value)
                    })
                });
                
                const data = await response.json();
                resDiv.style.display = 'block';
                
                if (response.ok) {
                    resDiv.className = 'success';
                    resDiv.innerHTML = `<strong>Posibilidades de victoria: ${data.winProbabilityPercentage}%</strong><br>
                                      <small>Tiempo de cálculo: ${data.executionTimeSeconds}s | Simulaciones: ${data.iterations}</small>`;
                } else {
                    resDiv.className = 'error';
                    resDiv.innerText = 'Error: ' + data.error;
                }
            } catch (err) {
                resDiv.style.display = 'block';
                resDiv.className = 'error';
                resDiv.innerText = 'Error de conexión.';
            } finally {
                btn.disabled = false;
                btn.innerText = 'Calcular';
            }
        }
    </script>
</body>
</html>
", "text/html; charset=utf-8"));

app.MapPost("/calculate", (CalculateRequest request) => {
    try {
        if (string.IsNullOrWhiteSpace(request.HoleCards)) throw new Exception("Debes tener 2 cartas en la mano.");
        if (request.Opponents < 1 || request.Opponents > 9) throw new Exception("El número de oponentes debe ser entre 1 y 9.");

        var holeParsed = ParseCards(request.HoleCards);
        if (holeParsed.Count != 2) throw new Exception("Debes tener 2 cartas en la mano.");

        var boardParsed = ParseCards(request.BoardCards ?? "");
        if (boardParsed.Count > 5) throw new Exception("La mesa no puede tener más de 5 cartas.");

        var allParsed = holeParsed.Concat(boardParsed).ToList();
        if (allParsed.Distinct().Count() != allParsed.Count) throw new Exception("Se detectaron cartas duplicadas.");
        
        var sw = Stopwatch.StartNew();
        
        var deckStrs = new List<string>();
        foreach(var r in "23456789TJQKA") foreach(var s in "hscd") deckStrs.Add($"{r}{s}");
        
        var knownStrs = new HashSet<string>(allParsed);
        var remainingDeck = deckStrs.Where(c => !knownStrs.Contains(c)).Select(c => Card.Parse(c)).ToArray();
        
        var hole = holeParsed.Select(Card.Parse).ToArray();
        var board = boardParsed.Select(Card.Parse).ToList();
        int missingBoard = 5 - board.Count;
        
        int wins = 0, ties = 0, iterations = 200000;
        var rnd = new Random();
        int cardsToDraw = missingBoard + (request.Opponents * 2);
        
        for(int i = 0; i < iterations; i++) {
            for(int j = 0; j < cardsToDraw; j++) {
                int swapIdx = rnd.Next(j, remainingDeck.Length);
                (remainingDeck[j], remainingDeck[swapIdx]) = (remainingDeck[swapIdx], remainingDeck[j]);
            }
            
            Card[] common = new Card[5];
            for(int b = 0; b < board.Count; b++) common[b] = board[b];
            for(int b = 0; b < missingBoard; b++) common[board.Count + b] = remainingDeck[b];

            Card[] heroFull = [hole[0], hole[1], .. common];
            int heroRank = HoldemHandEvaluator.GetHandRanking(heroFull);
            
            bool heroWins = true;
            bool isTie = false;
            int deckIdx = missingBoard;

            for(int o = 0; o < request.Opponents; o++) {
                Card[] oppFull = [remainingDeck[deckIdx++], remainingDeck[deckIdx++], .. common];
                int oppRank = HoldemHandEvaluator.GetHandRanking(oppFull);
                
                if (oppRank < heroRank) { heroWins = false; isTie = false; break; }
                if (oppRank == heroRank) isTie = true;
            }
            
            if (heroWins && !isTie) wins++;
            else if (heroWins && isTie) ties++;
        }
        
        sw.Stop();
        double prob = (wins + (ties > 0 ? (double)ties / (request.Opponents + 1) : 0)) / iterations;
        
        return Results.Ok(new CalculateResponse(
            Math.Round(prob * 100, 2),
            Math.Round(sw.Elapsed.TotalSeconds, 4),
            iterations,
            request.Opponents
        ));
    } catch(Exception ex) {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

public record CalculateRequest(string HoleCards, string? BoardCards, int Opponents);
public record CalculateResponse(double WinProbabilityPercentage, double ExecutionTimeSeconds, int Iterations, int Opponents);