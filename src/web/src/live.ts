import { HubConnection, HubConnectionBuilder } from "@microsoft/signalr";
import { apiUrl } from "./api";

export async function connectList(household: string, onChange: () => void): Promise<HubConnection> {
  const connection = new HubConnectionBuilder().withUrl(apiUrl("/hubs/list")).withAutomaticReconnect().build();
  connection.on("listChanged", onChange);
  await connection.start();
  await connection.invoke("Join", household);
  return connection;
}
