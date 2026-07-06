import { useCallback, useEffect, useState } from "react";
import {
  Table,
  TableHeader,
  TableRow,
  TableHeaderCell,
  TableBody,
  TableCell,
  Button,
  Spinner,
  Text,
  Card,
  MessageBar,
  MessageBarBody,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { ArrowClockwise24Regular, Bot24Regular } from "@fluentui/react-icons";
import { getWeatherForecast } from "../api/weatherApi";
import type { WeatherForecast } from "../api/types";

const useStyles = makeStyles({
  header: {
    display: "flex",
    alignItems: "center",
    gap: "10px",
    marginBottom: "8px",
  },
  intro: {
    color: tokens.colorNeutralForeground2,
    marginBottom: "16px",
    display: "block",
  },
  card: {
    maxWidth: "760px",
  },
  toolbar: {
    display: "flex",
    alignItems: "center",
    gap: "12px",
    marginBottom: "16px",
  },
  error: {
    marginBottom: "16px",
  },
});

export default function AgentPage() {
  const styles = useStyles();
  const [forecasts, setForecasts] = useState<WeatherForecast[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getWeatherForecast();
      setForecasts(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load weather forecast.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <div>
      <div className={styles.header}>
        <Bot24Regular />
        <Text size={600} weight="semibold">
          Weather Agent
        </Text>
      </div>
      <Text className={styles.intro}>
        This agent retrieves a 5-day forecast from the protected ReactApp.Api
        <code> /weatherforecast </code> endpoint using an MSAL access token.
      </Text>

      {error && (
        <MessageBar intent="error" className={styles.error}>
          <MessageBarBody>{error}</MessageBarBody>
        </MessageBar>
      )}

      <Card className={styles.card}>
        <div className={styles.toolbar}>
          <Button
            appearance="primary"
            icon={<ArrowClockwise24Regular />}
            onClick={() => void load()}
            disabled={loading}
          >
            Refresh
          </Button>
          {loading && <Spinner size="tiny" label="Loading…" />}
        </div>

        <Table aria-label="Weather forecast">
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Date</TableHeaderCell>
              <TableHeaderCell>Temp. (°C)</TableHeaderCell>
              <TableHeaderCell>Temp. (°F)</TableHeaderCell>
              <TableHeaderCell>Summary</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {forecasts.map((f) => (
              <TableRow key={f.date}>
                <TableCell>{f.date}</TableCell>
                <TableCell>{f.temperatureC}</TableCell>
                <TableCell>{f.temperatureF}</TableCell>
                <TableCell>{f.summary}</TableCell>
              </TableRow>
            ))}
            {!loading && forecasts.length === 0 && !error && (
              <TableRow>
                <TableCell colSpan={4}>
                  <Text italic>No forecast data.</Text>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </Card>
    </div>
  );
}
