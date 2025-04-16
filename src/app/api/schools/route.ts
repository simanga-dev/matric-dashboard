export async function POST(req: Request) {
  const { q } = await req.json();
  const url = process.env.MEILI_URL + "/indexes/schools/search";

  const res = await fetch(url, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${process.env.MEILI_TOKEN}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ q: q }),
  });

  const data = await res.json();
  return Response.json({ data: data.hits });
}
