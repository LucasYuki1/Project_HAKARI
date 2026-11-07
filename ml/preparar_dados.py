"""Unifica os CSVs trimestrais do SP156 e gera o recorte de reparos de infraestrutura do bairro.

Os arquivos do portal mudam de formato entre trimestres (separador, codificação e ordem dia/mês),
então cada um é lido com o seu formato e convertido para um esquema único. Logradouro, número,
CEP e coordenadas são descartados: o modelo trabalha no nível de distrito.

Uso:
    python preparar_dados.py
Entrada:  dados/raw/arquivo-final-*.csv  (baixar pelos links em dados/README.md)
Saída:    dados/sp156_rua_e_bairro.csv.gz
"""
from pathlib import Path

import pandas as pd

HERE = Path(__file__).resolve().parent
RAW = HERE / 'dados' / 'raw'
OUTPUT = HERE / 'dados' / 'sp156_rua_e_bairro.csv.gz'

# Formato de cada arquivo, conferido no cabeçalho e nas primeiras linhas.
ARQUIVOS = {
    '2025T4': ('arquivo-final-4-trim-25.csv', ';', 'latin1', '%d/%m/%Y'),
    '2026T1': ('arquivo-final-1-trim-26.csv', ';', 'latin1', '%d/%m/%Y'),
    '2026T2': ('arquivo-final-2-trim-26.csv', ';', 'latin1', '%d/%m/%Y'),
    '2026T3': ('arquivo-final-3-trim-26.csv', ',', 'utf-8', '%m/%d/%Y'),
}

COLUNAS = ['abertura', 'finalizacao', 'meio', 'tema', 'assunto', 'servico', 'status', 'orgao',
           'logradouro', 'numero', 'cep', 'lat', 'lon', 'prefeitura', 'distrito']

# Assuntos de reparo físico; PSIU, ambulantes, terrenos e eventos são fiscalização, não conserto.
ASSUNTOS_REPARO = [
    'Reparos em  Asfalto, Pontes, Viadutos e Túneis',
    'Fiação e Postes',
    'Drenagem de água de chuva',
    'Capinação e roçada de áreas verdes',
    'Praças',
    'Ruas, vilas, vielas e escadarias',
]


def ler_trimestre(trimestre, arquivo, sep, encoding, formato_data):
    df = pd.read_csv(RAW / arquivo, sep=sep, encoding=encoding, dtype=str, header=0, names=COLUNAS)
    df = df[(df['tema'] == 'Rua e bairro') & df['assunto'].isin(ASSUNTOS_REPARO)].copy()
    df['abertura'] = pd.to_datetime(df['abertura'], format=formato_data, errors='coerce')
    df['finalizacao'] = pd.to_datetime(df['finalizacao'], format=formato_data, errors='coerce')
    df['trimestre'] = trimestre
    # O portal não informa a data de extração; a última finalização do arquivo inteiro é a melhor aproximação.
    todas = pd.read_csv(RAW / arquivo, sep=sep, encoding=encoding, dtype=str, usecols=[1])
    df['data_extracao'] = pd.to_datetime(todas.iloc[:, 0], format=formato_data, errors='coerce').max()
    return df


def main():
    partes = []
    for trimestre, (arquivo, sep, encoding, formato_data) in ARQUIVOS.items():
        parte = ler_trimestre(trimestre, arquivo, sep, encoding, formato_data)
        print(f'{trimestre}: {len(parte):>6} solicitações, extração ~{parte["data_extracao"].iloc[0].date()}')
        partes.append(parte)

    df = pd.concat(partes, ignore_index=True)
    df = df.drop(columns=['tema', 'logradouro', 'numero', 'cep', 'lat', 'lon'])
    df.to_csv(OUTPUT, index=False, compression='gzip')
    print(f'Total: {len(df)} solicitações em {OUTPUT.name}')


if __name__ == '__main__':
    main()
