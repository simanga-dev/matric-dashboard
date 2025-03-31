export default async function Page({
  params,
}: {
  params: Promise<{ emis_number: string }>;
}) {
  const test = await params;

  return <h1>My Page {test.emis_number}</h1>;
}
